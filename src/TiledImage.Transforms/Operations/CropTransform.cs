using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Fast crop transform using memory copy.
/// No interpolation required.
/// </summary>
public sealed class CropTransform : ITransformStep
{
    private readonly Rect2L _region;

    public CropTransform(Rect2L region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentException("Crop region must have positive dimensions");
        _region = region;
    }

    public CropTransform(long x, long y, long width, long height)
        : this(new Rect2L(x, y, width, height))
    {
    }

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => new(_region.Width, _region.Height);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRegion(provider);

        long outputWidth = _region.Width;
        long outputHeight = _region.Height;
        int bytesPerPixel = provider.BytesPerPixel;
        long totalBytes = outputWidth * outputHeight * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ExecuteInternal(provider, buffer, z, bytesPerPixel, 0, progress, cancellationToken);
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
        ValidateRegion(provider);

        long outputWidth = _region.Width;
        long outputHeight = _region.Height;
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;
        long sliceBytes = outputWidth * outputHeight * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = zSlice * sliceBytes;
                ExecuteInternal(provider, buffer, zSlice, bytesPerPixel, sliceOffset);

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
    /// Crop an image region. Fast path using memory copy.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        Rect2L region,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new CropTransform(region).Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Crop all Z slices.
    /// </summary>
    public static IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        Rect2L region,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new CropTransform(region).ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Calculate output size after crop.
    /// </summary>
    public static Size2L CalculateOutputSize(Rect2L region)
        => new(region.Width, region.Height);

    #endregion

    #region Private Methods

    private void ValidateRegion(ITileDataProvider provider)
    {
        if (_region.X < 0 || _region.Y < 0)
            throw new ArgumentException("Crop region must have non-negative origin");

        if (_region.X + _region.Width > provider.ImageWidth ||
            _region.Y + _region.Height > provider.ImageHeight)
            throw new ArgumentException("Crop region exceeds image bounds");
    }

    private void ExecuteInternal(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        long z,
        int bytesPerPixel,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        long outputWidth = _region.Width;
        long outputHeight = _region.Height;
        int rowBytes = (int)(outputWidth * bytesPerPixel);
        byte[] rowBuffer = new byte[rowBytes];

        for (long row = 0; row < outputHeight; row++)
        {
            if (row % 100 == 0)
                cancellationToken.ThrowIfCancellationRequested();

            provider.ReadPixels(_region.X, _region.Y + row, z, (int)outputWidth, 1, rowBuffer);

            long destOffset = bufferOffset + row * rowBytes;
            outputBuffer.Write(destOffset, rowBuffer, 0, rowBytes);

            progress?.Report((double)(row + 1) / outputHeight);
        }
    }

    #endregion
}
