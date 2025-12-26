using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Transforms.Sampling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Scale transform with fast path for integer downscaling.
/// </summary>
public sealed class ScaleTransform : ITransformStep
{
    private const int TileSize = 512;

    private readonly double _factor;
    private readonly InterpolationMode _interpolation;

    public ScaleTransform(double factor, InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        if (factor <= 0)
            throw new ArgumentException("Scale factor must be positive", nameof(factor));
        _factor = factor;
        _interpolation = interpolation;
    }

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => CalculateOutputSize(inputWidth, inputHeight, _factor);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Math.Abs(_factor - 1.0) < 0.001)
            return CopyImage(provider, z, bufferType, progress, cancellationToken);

        if (_factor < 1.0 && IsIntegerDownscale(_factor, out int downscaleFactor))
            return ExecuteDownInteger(provider, downscaleFactor, z, bufferType, progress, cancellationToken);

        return ExecuteWithInterpolation(provider, z, bufferType, progress, cancellationToken);
    }

    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        long srcWidth = provider.ImageWidth;
        long srcHeight = provider.ImageHeight;
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;

        var outSize = CalculateOutputSize(srcWidth, srcHeight, _factor);
        long sliceBytes = outSize.Width * outSize.Height * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            int downscaleFactor = 0;
            bool isIntegerDownscale = _factor < 1.0 && IsIntegerDownscale(_factor, out downscaleFactor);

            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = zSlice * sliceBytes;

                if (Math.Abs(_factor - 1.0) < 0.001)
                {
                    CopySlice(provider, buffer, zSlice, sliceOffset);
                }
                else if (isIntegerDownscale)
                {
                    ExecuteDownIntegerInternal(provider, buffer, srcWidth, srcHeight, outSize.Width, outSize.Height,
                        bytesPerPixel, downscaleFactor, zSlice, sliceOffset);
                }
                else
                {
                    ExecuteWithInterpolationInternal(provider, buffer, outSize.Width, outSize.Height,
                        bytesPerPixel, zSlice, sliceOffset, cancellationToken);
                }

                progress?.Report((double)(zSlice + 1) / depth);
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    #endregion

    #region Static Convenience Methods

    /// <summary>
    /// Scale image by a factor. Uses fast path for integer downscaling.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        double factor,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new ScaleTransform(factor, interpolation).Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Fast integer downscaling (1/2, 1/4, 1/8, etc.).
    /// </summary>
    public static IPixelBuffer ExecuteDownInteger(
        ITileDataProvider provider,
        int factor,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (factor < 2)
            throw new ArgumentException("Downscale factor must be >= 2", nameof(factor));

        long srcWidth = provider.ImageWidth;
        long srcHeight = provider.ImageHeight;
        int bytesPerPixel = provider.BytesPerPixel;

        long dstWidth = srcWidth / factor;
        long dstHeight = srcHeight / factor;
        long totalBytes = dstWidth * dstHeight * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ExecuteDownIntegerInternal(provider, buffer, srcWidth, srcHeight, dstWidth, dstHeight,
                bytesPerPixel, factor, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Scale all Z slices.
    /// </summary>
    public static IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        double factor,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new ScaleTransform(factor, interpolation).ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Calculate output size after scaling.
    /// </summary>
    public static Size2L CalculateOutputSize(long width, long height, double factor)
    {
        long newWidth = (long)Math.Ceiling(width * factor);
        long newHeight = (long)Math.Ceiling(height * factor);
        return new Size2L(Math.Max(1, newWidth), Math.Max(1, newHeight));
    }

    /// <summary>
    /// Compute scale matrix.
    /// </summary>
    public static Matrix3x3 ComputeMatrix(double factor)
    {
        return Matrix3x3.CreateScale((float)factor, (float)factor);
    }

    #endregion

    #region Private Methods

    private static void ExecuteDownIntegerInternal(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        long srcWidth,
        long srcHeight,
        long dstWidth,
        long dstHeight,
        int bytesPerPixel,
        int factor,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int srcRowBytes = (int)(srcWidth * bytesPerPixel);
        byte[] srcRow = new byte[srcRowBytes];
        int dstRowBytes = (int)(dstWidth * bytesPerPixel);
        byte[] dstRow = new byte[dstRowBytes];

        for (long dstY = 0; dstY < dstHeight; dstY++)
        {
            if (dstY % 100 == 0)
                cancellationToken.ThrowIfCancellationRequested();

            long srcY = dstY * factor;
            provider.ReadPixels(0, srcY, z, (int)srcWidth, 1, srcRow);

            for (long dstX = 0; dstX < dstWidth; dstX++)
            {
                long srcX = dstX * factor;
                int srcOffset = (int)(srcX * bytesPerPixel);
                int dstOffset = (int)(dstX * bytesPerPixel);

                for (int b = 0; b < bytesPerPixel; b++)
                {
                    dstRow[dstOffset + b] = srcRow[srcOffset + b];
                }
            }

            long destOffset = bufferOffset + dstY * dstRowBytes;
            outputBuffer.Write(destOffset, dstRow, 0, dstRowBytes);

            progress?.Report((double)(dstY + 1) / dstHeight);
        }
    }

    private IPixelBuffer ExecuteWithInterpolation(
        ITileDataProvider provider,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outSize = CalculateOutputSize(provider.ImageWidth, provider.ImageHeight, _factor);
        var matrix = ComputeMatrix(_factor);

        return AffineTransform.Execute(provider, matrix, outSize.Width, outSize.Height,
            _interpolation, z, bufferType, progress, cancellationToken);
    }

    private void ExecuteWithInterpolationInternal(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        long outputWidth,
        long outputHeight,
        int bytesPerPixel,
        long z,
        long bufferOffset,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        var matrix = ComputeMatrix(_factor);
        Matrix3x3 inverseMatrix;
        if (!matrix.TryInvert(out inverseMatrix))
        {
            inverseMatrix = Matrix3x3.Identity;
        }

        int tilesX = (int)Math.Ceiling((double)outputWidth / TileSize);
        int tilesY = (int)Math.Ceiling((double)outputHeight / TileSize);
        int totalTiles = tilesX * tilesY;
        int completedTiles = 0;

        Parallel.For(0, totalTiles, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, tileIndex =>
        {
            int tileX = tileIndex % tilesX;
            int tileY = tileIndex / tilesX;

            int outX = tileX * TileSize;
            int outY = tileY * TileSize;
            int outTileWidth = (int)Math.Min(TileSize, outputWidth - outX);
            int outTileHeight = (int)Math.Min(TileSize, outputHeight - outY);

            if (outTileWidth <= 0 || outTileHeight <= 0) return;

            int stride = outTileWidth * bytesPerPixel;
            byte[] tileBuffer = new byte[outTileWidth * outTileHeight * bytesPerPixel];

            var sampler = new TilePixelSampler(provider, z);

            PixelResampler.ResampleRegion(
                sampler,
                inverseMatrix,
                tileBuffer,
                outX,
                outY,
                outTileWidth,
                outTileHeight,
                stride,
                bytesPerPixel,
                provider.PixelFormat,
                _interpolation);

            for (int row = 0; row < outTileHeight; row++)
            {
                long destOffset = bufferOffset + ((outY + row) * outputWidth + outX) * bytesPerPixel;
                int srcOffset = row * stride;
                outputBuffer.Write(destOffset, tileBuffer, srcOffset, stride);
            }

            int completed = Interlocked.Increment(ref completedTiles);
            progress?.Report((double)completed / totalTiles);
        });
    }

    private static IPixelBuffer CopyImage(
        ITileDataProvider provider,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long width = provider.ImageWidth;
        long height = provider.ImageHeight;
        int bytesPerPixel = provider.BytesPerPixel;
        long totalBytes = width * height * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            CopySlice(provider, buffer, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static void CopySlice(
        ITileDataProvider provider,
        IPixelBuffer buffer,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        long width = provider.ImageWidth;
        long height = provider.ImageHeight;
        int bytesPerPixel = provider.BytesPerPixel;
        int rowBytes = (int)(width * bytesPerPixel);
        byte[] rowBuffer = new byte[rowBytes];

        for (long y = 0; y < height; y++)
        {
            if (y % 100 == 0)
                cancellationToken.ThrowIfCancellationRequested();

            provider.ReadPixels(0, y, z, (int)width, 1, rowBuffer);
            buffer.Write(bufferOffset + y * rowBytes, rowBuffer, 0, rowBytes);

            progress?.Report((double)(y + 1) / height);
        }
    }

    private static bool IsIntegerDownscale(double factor, out int downscaleFactor)
    {
        double inverse = 1.0 / factor;
        int rounded = (int)Math.Round(inverse);

        if (rounded >= 2 && Math.Abs(inverse - rounded) < 0.001 && IsPowerOfTwo(rounded))
        {
            downscaleFactor = rounded;
            return true;
        }

        downscaleFactor = 0;
        return false;
    }

    private static bool IsPowerOfTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }

    #endregion
}
