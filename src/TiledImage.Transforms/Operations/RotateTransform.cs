using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Transforms.Sampling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Rotation transform with fast path for 90/180/270 degrees.
/// </summary>
public sealed class RotateTransform : ITransformStep
{
    private const int TileSize = 512;

    private readonly double _degrees;
    private readonly InterpolationMode _interpolation;

    public RotateTransform(double degrees, InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _degrees = NormalizeAngle(degrees);
        _interpolation = interpolation;
    }

    /// <summary>
    /// Create a 90-degree clockwise rotation transform.
    /// </summary>
    public static RotateTransform Rotate90() => new(90);

    /// <summary>
    /// Create a 180-degree rotation transform.
    /// </summary>
    public static RotateTransform Rotate180() => new(180);

    /// <summary>
    /// Create a 270-degree clockwise (90 counter-clockwise) rotation transform.
    /// </summary>
    public static RotateTransform Rotate270() => new(270);

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => CalculateOutputSize(inputWidth, inputHeight, _degrees);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsOrthogonalAngle(_degrees, out int orthogonalDegrees))
        {
            if (orthogonalDegrees == 0)
                return CopyImage(provider, z, bufferType, progress, cancellationToken);
            return ExecuteOrthogonal(provider, orthogonalDegrees, z, bufferType, progress, cancellationToken);
        }

        return ExecuteAffine(provider, z, bufferType, progress, cancellationToken);
    }

    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsOrthogonalAngle(_degrees, out int orthogonalDegrees))
        {
            if (orthogonalDegrees == 0)
                return CopyImageAll(provider, bufferType, progress, cancellationToken);
            return ExecuteOrthogonalAll(provider, orthogonalDegrees, bufferType, progress, cancellationToken);
        }

        return ExecuteAffineAll(provider, bufferType, progress, cancellationToken);
    }

    #endregion

    #region Static Convenience Methods

    /// <summary>
    /// Rotate image 90 degrees clockwise. Fast path using index rearrangement.
    /// </summary>
    public static IPixelBuffer Execute90(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate90().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate image 180 degrees. Fast path using index rearrangement.
    /// </summary>
    public static IPixelBuffer Execute180(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate180().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate image 270 degrees clockwise (90 degrees counter-clockwise). Fast path.
    /// </summary>
    public static IPixelBuffer Execute270(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate270().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate image by arbitrary angle. Uses interpolation.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        double degrees,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new RotateTransform(degrees, interpolation).Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate all Z slices by 90 degrees.
    /// </summary>
    public static IPixelBuffer Execute90All(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate90().ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate all Z slices by 180 degrees.
    /// </summary>
    public static IPixelBuffer Execute180All(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate180().ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Rotate all Z slices by 270 degrees.
    /// </summary>
    public static IPixelBuffer Execute270All(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Rotate270().ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Calculate output size after rotation.
    /// </summary>
    public static Size2L CalculateOutputSize(long width, long height, double degrees)
    {
        degrees = NormalizeAngle(degrees);

        if (IsOrthogonalAngle(degrees, out int orthogonalDegrees))
        {
            return orthogonalDegrees switch
            {
                90 or 270 => new Size2L(height, width),
                _ => new Size2L(width, height)
            };
        }

        double radians = degrees * Math.PI / 180.0;
        double absCos = Math.Abs(Math.Cos(radians));
        double absSin = Math.Abs(Math.Sin(radians));

        long newWidth = (long)Math.Ceiling(width * absCos + height * absSin);
        long newHeight = (long)Math.Ceiling(width * absSin + height * absCos);

        return new Size2L(newWidth, newHeight);
    }

    /// <summary>
    /// Compute rotation matrix for arbitrary angle.
    /// </summary>
    public static Matrix3x3 ComputeMatrix(long width, long height, double degrees)
    {
        double thetaRad = degrees * Math.PI / 180.0;
        double absCosT = Math.Abs(Math.Cos(thetaRad));
        double absSinT = Math.Abs(Math.Sin(thetaRad));

        long newWidth = (long)Math.Ceiling(width * absCosT + height * absSinT);
        long newHeight = (long)Math.Ceiling(width * absSinT + height * absCosT);

        float preCenterX = (width - 1) / 2.0f;
        float preCenterY = (height - 1) / 2.0f;
        float postCenterX = (newWidth - 1) / 2.0f;
        float postCenterY = (newHeight - 1) / 2.0f;

        var rotationMatrix = Matrix3x3.CreateRotationAt((float)thetaRad, preCenterX, preCenterY);
        float offsetX = postCenterX - preCenterX;
        float offsetY = postCenterY - preCenterY;

        return Matrix3x3.CreateTranslation(offsetX, offsetY) * rotationMatrix;
    }

    #endregion

    #region Orthogonal Rotation

    private IPixelBuffer ExecuteOrthogonal(
        ITileDataProvider provider,
        int degrees,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long srcWidth = provider.ImageWidth;
        long srcHeight = provider.ImageHeight;
        int bytesPerPixel = provider.BytesPerPixel;

        var outSize = CalculateOutputSize(srcWidth, srcHeight, degrees);
        long totalBytes = outSize.Width * outSize.Height * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ExecuteOrthogonalInternal(provider, buffer, srcWidth, srcHeight, outSize.Width, outSize.Height,
                bytesPerPixel, degrees, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private IPixelBuffer ExecuteOrthogonalAll(
        ITileDataProvider provider,
        int degrees,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long srcWidth = provider.ImageWidth;
        long srcHeight = provider.ImageHeight;
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;

        var outSize = CalculateOutputSize(srcWidth, srcHeight, degrees);
        long sliceBytes = outSize.Width * outSize.Height * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = zSlice * sliceBytes;
                ExecuteOrthogonalInternal(provider, buffer, srcWidth, srcHeight, outSize.Width, outSize.Height,
                    bytesPerPixel, degrees, zSlice, sliceOffset);

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

    private static void ExecuteOrthogonalInternal(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        long srcWidth,
        long srcHeight,
        long dstWidth,
        long dstHeight,
        int bytesPerPixel,
        int degrees,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int srcRowBytes = (int)(srcWidth * bytesPerPixel);
        byte[] srcRow = new byte[srcRowBytes];

        for (long srcY = 0; srcY < srcHeight; srcY++)
        {
            if (srcY % 100 == 0)
                cancellationToken.ThrowIfCancellationRequested();

            provider.ReadPixels(0, srcY, z, (int)srcWidth, 1, srcRow);

            for (long srcX = 0; srcX < srcWidth; srcX++)
            {
                long dstX, dstY;
                switch (degrees)
                {
                    case 90:
                        dstX = srcHeight - 1 - srcY;
                        dstY = srcX;
                        break;
                    case 180:
                        dstX = srcWidth - 1 - srcX;
                        dstY = srcHeight - 1 - srcY;
                        break;
                    case 270:
                        dstX = srcY;
                        dstY = srcWidth - 1 - srcX;
                        break;
                    default:
                        dstX = srcX;
                        dstY = srcY;
                        break;
                }

                int srcOffset = (int)(srcX * bytesPerPixel);
                long dstOffset = bufferOffset + (dstY * dstWidth + dstX) * bytesPerPixel;

                outputBuffer.Write(dstOffset, srcRow, srcOffset, bytesPerPixel);
            }

            progress?.Report((double)(srcY + 1) / srcHeight);
        }
    }

    #endregion

    #region Affine Rotation

    private IPixelBuffer ExecuteAffine(
        ITileDataProvider provider,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outSize = CalculateOutputSize(provider.ImageWidth, provider.ImageHeight, _degrees);
        var matrix = ComputeMatrix(provider.ImageWidth, provider.ImageHeight, _degrees);

        return AffineTransform.Execute(provider, matrix, outSize.Width, outSize.Height,
            _interpolation, z, bufferType, progress, cancellationToken);
    }

    private IPixelBuffer ExecuteAffineAll(
        ITileDataProvider provider,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outSize = CalculateOutputSize(provider.ImageWidth, provider.ImageHeight, _degrees);
        var matrix = ComputeMatrix(provider.ImageWidth, provider.ImageHeight, _degrees);

        return AffineTransform.ExecuteAll(provider, matrix, outSize.Width, outSize.Height,
            _interpolation, bufferType, progress, cancellationToken);
    }

    #endregion

    #region Helper Methods

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
            int rowBytes = (int)(width * bytesPerPixel);
            byte[] rowBuffer = new byte[rowBytes];

            for (long y = 0; y < height; y++)
            {
                if (y % 100 == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                provider.ReadPixels(0, y, z, (int)width, 1, rowBuffer);
                buffer.Write(y * rowBytes, rowBuffer, 0, rowBytes);

                progress?.Report((double)(y + 1) / height);
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static IPixelBuffer CopyImageAll(
        ITileDataProvider provider,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long width = provider.ImageWidth;
        long height = provider.ImageHeight;
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;
        long sliceBytes = width * height * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            int rowBytes = (int)(width * bytesPerPixel);
            byte[] rowBuffer = new byte[rowBytes];

            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long sliceOffset = zSlice * sliceBytes;

                for (long y = 0; y < height; y++)
                {
                    provider.ReadPixels(0, y, zSlice, (int)width, 1, rowBuffer);
                    buffer.Write(sliceOffset + y * rowBytes, rowBuffer, 0, rowBytes);
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

    private static double NormalizeAngle(double degrees)
    {
        degrees %= 360;
        if (degrees < 0) degrees += 360;
        return degrees;
    }

    private static bool IsOrthogonalAngle(double degrees, out int orthogonalDegrees)
    {
        const double tolerance = 0.001;

        if (Math.Abs(degrees) < tolerance || Math.Abs(degrees - 360) < tolerance)
        {
            orthogonalDegrees = 0;
            return true;
        }
        if (Math.Abs(degrees - 90) < tolerance)
        {
            orthogonalDegrees = 90;
            return true;
        }
        if (Math.Abs(degrees - 180) < tolerance)
        {
            orthogonalDegrees = 180;
            return true;
        }
        if (Math.Abs(degrees - 270) < tolerance)
        {
            orthogonalDegrees = 270;
            return true;
        }

        orthogonalDegrees = 0;
        return false;
    }

    #endregion
}
