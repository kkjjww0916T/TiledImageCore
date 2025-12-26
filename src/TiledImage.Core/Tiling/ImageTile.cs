using System.Buffers;
using TiledImage.Core.Types;

namespace TiledImage.Core.Tiling;

/// <summary>
/// Platform-independent tile data containing raw pixel information.
/// This class holds only the data, not the rendering representation.
/// </summary>
public class ImageTile : IDisposable
{
    private ArrayPool<byte>? _arrayPool;
    private bool _isPooledData;
    private bool _disposed;

    /// <summary>
    /// X coordinate of the tile in original image coordinates.
    /// </summary>
    public long X { get; }

    /// <summary>
    /// Y coordinate of the tile in original image coordinates.
    /// </summary>
    public long Y { get; }

    /// <summary>
    /// Width of the tile in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the tile in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// MipMap level (0 = original, 1 = half, etc.).
    /// </summary>
    public int MipLevel { get; }

    /// <summary>
    /// Raw pixel data in BGRA format.
    /// </summary>
    public byte[]? PixelData { get; private set; }

    /// <summary>
    /// Actual length of valid data in PixelData array.
    /// May be less than PixelData.Length when using ArrayPool.
    /// </summary>
    public int PixelDataLength { get; private set; }

    /// <summary>
    /// Pixel format of the data.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Last time this tile was accessed.
    /// </summary>
    public DateTime LastAccessed { get; private set; }

    /// <summary>
    /// Whether the tile has loaded pixel data.
    /// </summary>
    public bool IsLoaded => PixelData != null;

    /// <summary>
    /// Stride (bytes per row) of the pixel data.
    /// </summary>
    public int Stride => Width * Format.GetBytesPerPixel();

    public ImageTile(long x, long y, int width, int height, int mipLevel = 0, PixelFormat format = PixelFormat.Bgra32)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        MipLevel = mipLevel;
        Format = format;
        LastAccessed = DateTime.UtcNow;
    }

    /// <summary>
    /// Set pixel data from an ArrayPool buffer.
    /// </summary>
    public void SetPixelData(byte[] data, int length, ArrayPool<byte> pool)
    {
        ReturnPooledData();

        PixelData = data;
        PixelDataLength = length;
        _arrayPool = pool;
        _isPooledData = true;
        LastAccessed = DateTime.UtcNow;
    }

    /// <summary>
    /// Set pixel data from a non-pooled array.
    /// </summary>
    public void SetPixelData(byte[] data)
    {
        ReturnPooledData();

        PixelData = data;
        PixelDataLength = data.Length;
        _arrayPool = null;
        _isPooledData = false;
        LastAccessed = DateTime.UtcNow;
    }

    /// <summary>
    /// Update last accessed time.
    /// </summary>
    public void UpdateLastAccessed()
    {
        LastAccessed = DateTime.UtcNow;
    }

    private void ReturnPooledData()
    {
        if (_isPooledData && PixelData != null && _arrayPool != null)
        {
            _arrayPool.Return(PixelData);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ReturnPooledData();
        PixelData = null;
        _arrayPool = null;
        _isPooledData = false;
    }
}
