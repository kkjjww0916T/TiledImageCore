using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Transforms.Sampling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Affine transformation with matrix-based pixel mapping.
/// Applies arbitrary affine transforms (scale, rotate, shear, translate) using a 3x3 matrix.
/// </summary>
public sealed class AffineTransform : ITransformStep
{
    private const int TileSize = 512;

    private readonly Matrix3x3 _matrix;
    private readonly long _outputWidth;
    private readonly long _outputHeight;
    private readonly InterpolationMode _interpolation;

    public AffineTransform(Matrix3x3 matrix, long outputWidth, long outputHeight,
        InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _matrix = matrix;
        _outputWidth = outputWidth;
        _outputHeight = outputHeight;
        _interpolation = interpolation;
    }

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => new(_outputWidth, _outputHeight);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Matrix3x3 inverseMatrix;
        if (!_matrix.TryInvert(out inverseMatrix))
        {
            inverseMatrix = Matrix3x3.Identity;
        }

        var transform = new AffineTransformResult(_matrix, inverseMatrix, _outputWidth, _outputHeight);
        return ExecuteInternal(provider, transform, z, bufferType, progress, cancellationToken);
    }

    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Matrix3x3 inverseMatrix;
        if (!_matrix.TryInvert(out inverseMatrix))
        {
            inverseMatrix = Matrix3x3.Identity;
        }

        var transform = new AffineTransformResult(_matrix, inverseMatrix, _outputWidth, _outputHeight);
        return ExecuteAllInternal(provider, transform, bufferType, progress, cancellationToken);
    }

    #endregion

    #region Static Convenience Methods

    /// <summary>
    /// Execute affine transformation on a single Z slice using a matrix.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        Matrix3x3 matrix,
        long outputWidth,
        long outputHeight,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new AffineTransform(matrix, outputWidth, outputHeight, interpolation)
            .Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Execute affine transformation on all Z slices using a matrix.
    /// </summary>
    public static IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        Matrix3x3 matrix,
        long outputWidth,
        long outputHeight,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new AffineTransform(matrix, outputWidth, outputHeight, interpolation)
            .ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    #endregion

    #region Internal Implementation

    private IPixelBuffer ExecuteInternal(
        ITileDataProvider provider,
        AffineTransformResult transform,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long outputWidth = transform.OutputWidth;
        long outputHeight = transform.OutputHeight;
        int bytesPerPixel = provider.BytesPerPixel;
        long totalBytes = outputWidth * outputHeight * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ResampleSlice(provider, buffer, transform, outputWidth, outputHeight,
                bytesPerPixel, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private IPixelBuffer ExecuteAllInternal(
        ITileDataProvider provider,
        AffineTransformResult transform,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long outputWidth = transform.OutputWidth;
        long outputHeight = transform.OutputHeight;
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;
        long sliceBytes = outputWidth * outputHeight * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            for (long z = 0; z < depth; z++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = z * sliceBytes;
                ResampleSlice(provider, buffer, transform, outputWidth, outputHeight,
                    bytesPerPixel, z, sliceOffset);

                progress?.Report((double)(z + 1) / depth);
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private void ResampleSlice(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        AffineTransformResult transform,
        long outputWidth,
        long outputHeight,
        int bytesPerPixel,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
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
                transform.Inverse,
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

    #endregion
}
