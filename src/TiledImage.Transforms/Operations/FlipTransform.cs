using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Fast flip transform using index reversal.
/// No interpolation required.
/// </summary>
public sealed class FlipTransform : ITransformStep
{
    private readonly bool _horizontal;
    private readonly bool _vertical;

    public FlipTransform(bool horizontal, bool vertical)
    {
        if (!horizontal && !vertical)
            throw new ArgumentException("At least one flip direction must be specified");
        _horizontal = horizontal;
        _vertical = vertical;
    }

    /// <summary>
    /// Create a horizontal flip transform (mirror along vertical axis).
    /// </summary>
    public static FlipTransform Horizontal() => new(horizontal: true, vertical: false);

    /// <summary>
    /// Create a vertical flip transform (mirror along horizontal axis).
    /// </summary>
    public static FlipTransform Vertical() => new(horizontal: false, vertical: true);

    /// <summary>
    /// Create a transform that flips both horizontally and vertically (equivalent to 180 rotation).
    /// </summary>
    public static FlipTransform Both() => new(horizontal: true, vertical: true);

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => new(inputWidth, inputHeight);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        long width = provider.ImageWidth;
        long height = provider.ImageHeight;
        int bytesPerPixel = provider.BytesPerPixel;
        long totalBytes = width * height * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ExecuteInternal(provider, buffer, width, height, bytesPerPixel, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
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
            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = zSlice * sliceBytes;
                ExecuteInternal(provider, buffer, width, height, bytesPerPixel, zSlice, sliceOffset);

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
    /// Flip image horizontally (mirror along vertical axis).
    /// </summary>
    public static IPixelBuffer ExecuteHorizontal(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Horizontal().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Flip image vertically (mirror along horizontal axis).
    /// </summary>
    public static IPixelBuffer ExecuteVertical(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Vertical().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Flip image both horizontally and vertically (equivalent to 180 rotation).
    /// </summary>
    public static IPixelBuffer ExecuteBoth(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Both().Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Flip all Z slices horizontally.
    /// </summary>
    public static IPixelBuffer ExecuteHorizontalAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Horizontal().ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Flip all Z slices vertically.
    /// </summary>
    public static IPixelBuffer ExecuteVerticalAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Vertical().ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    #endregion

    #region Private Methods

    private void ExecuteInternal(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        long width,
        long height,
        int bytesPerPixel,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int rowBytes = (int)(width * bytesPerPixel);
        byte[] srcRow = new byte[rowBytes];
        byte[] dstRow = _horizontal ? new byte[rowBytes] : srcRow;

        for (long srcY = 0; srcY < height; srcY++)
        {
            if (srcY % 100 == 0)
                cancellationToken.ThrowIfCancellationRequested();

            provider.ReadPixels(0, srcY, z, (int)width, 1, srcRow);

            if (_horizontal)
            {
                FlipRowHorizontal(srcRow, dstRow, (int)width, bytesPerPixel);
            }

            long dstY = _vertical ? (height - 1 - srcY) : srcY;
            long destOffset = bufferOffset + dstY * rowBytes;

            outputBuffer.Write(destOffset, dstRow, 0, rowBytes);

            progress?.Report((double)(srcY + 1) / height);
        }
    }

    private static void FlipRowHorizontal(byte[] src, byte[] dst, int width, int bytesPerPixel)
    {
        for (int x = 0; x < width; x++)
        {
            int srcOffset = x * bytesPerPixel;
            int dstOffset = (width - 1 - x) * bytesPerPixel;

            for (int b = 0; b < bytesPerPixel; b++)
            {
                dst[dstOffset + b] = src[srcOffset + b];
            }
        }
    }

    #endregion
}
