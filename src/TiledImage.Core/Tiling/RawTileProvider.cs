using TiledImage.Core.Memory;
using TiledImage.Core.Types;

namespace TiledImage.Core.Tiling;

/// <summary>
/// Tile data provider for raw pixel data.
/// Uses IPixelBuffer for memory abstraction (memory-mapped or unmanaged pointer).
/// </summary>
public class RawTileProvider : ITileDataProvider
{
    private readonly IPixelBuffer _buffer;
    private bool _disposed;

    /// <inheritdoc/>
    public long ImageWidth { get; }

    /// <inheritdoc/>
    public long ImageHeight { get; }

    /// <inheritdoc/>
    public long ImageDepth { get; }

    /// <inheritdoc/>
    public PixelFormat PixelFormat { get; }

    /// <inheritdoc/>
    public int BytesPerPixel => PixelFormat.GetBytesPerPixel();

    /// <inheritdoc/>
    public bool IsReady => !_disposed && (ImageWidth > 0 && ImageHeight > 0);

    /// <summary>
    /// Create provider from an IPixelBuffer.
    /// Takes ownership of the buffer.
    /// </summary>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="depth">Image depth (default 1 for 2D images)</param>
    /// <param name="format">Pixel format of the raw data</param>
    /// <param name="buffer">Pixel buffer (ownership transferred)</param>
    public RawTileProvider(long width, long height, long depth, PixelFormat format, IPixelBuffer buffer)
    {
        ImageWidth = width;
        ImageHeight = height;
        ImageDepth = depth;
        PixelFormat = format;
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    /// <inheritdoc/>
    public void ReadTileData(long pixelX, long pixelY, long z, int tileWidth, int tileHeight, byte[] buffer)
    {
        if (_disposed) return;

        long sliceSize = ImageWidth * ImageHeight * BytesPerPixel;
        long sliceOffset = z * sliceSize;
        int rowBytes = tileWidth * BytesPerPixel;

        for (int row = 0; row < tileHeight; row++)
        {
            long sourceOffset = sliceOffset + ((pixelY + row) * ImageWidth + pixelX) * BytesPerPixel;
            int destOffset = row * rowBytes;
            _buffer.Read(sourceOffset, buffer, destOffset, rowBytes);
        }
    }

    /// <inheritdoc/>
    public void ReadPixels(long x, long y, long z, int width, int height, byte[] buffer)
    {
        if (_disposed) return;

        long sliceSize = ImageWidth * ImageHeight * BytesPerPixel;
        long sliceOffset = z * sliceSize;
        int rowBytes = width * BytesPerPixel;

        for (int row = 0; row < height; row++)
        {
            long sourceOffset = sliceOffset + ((y + row) * ImageWidth + x) * BytesPerPixel;
            int destOffset = row * rowBytes;
            _buffer.Read(sourceOffset, buffer, destOffset, rowBytes);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _buffer.Dispose();
    }
}
