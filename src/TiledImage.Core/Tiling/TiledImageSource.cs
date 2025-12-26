using TiledImage.Core.Types;

namespace TiledImage.Core.Tiling;

/// <summary>
/// Provides tile-based access to large images.
/// Manages tile size and delegates data access to ITileDataProvider.
/// </summary>
public class TiledImageSource : IDisposable
{
    private readonly ITileDataProvider _provider;
    private readonly int _tileSize;
    private bool _disposed;

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public long ImageWidth => _provider.ImageWidth;

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public long ImageHeight => _provider.ImageHeight;

    /// <summary>
    /// Image depth (number of Z slices). 1 for 2D images.
    /// </summary>
    public long ImageDepth => _provider.ImageDepth;

    /// <summary>
    /// Pixel format of the image data.
    /// </summary>
    public PixelFormat PixelFormat => _provider.PixelFormat;

    /// <summary>
    /// Bytes per pixel, derived from PixelFormat.
    /// </summary>
    public int BytesPerPixel => _provider.BytesPerPixel;

    /// <summary>
    /// Size of each tile in pixels.
    /// </summary>
    public int TileSize => _tileSize;

    /// <summary>
    /// Number of tiles in X direction.
    /// </summary>
    public int TilesX => (int)Math.Ceiling((double)ImageWidth / _tileSize);

    /// <summary>
    /// Number of tiles in Y direction.
    /// </summary>
    public int TilesY => (int)Math.Ceiling((double)ImageHeight / _tileSize);

    /// <summary>
    /// Whether the image source is loaded and ready.
    /// </summary>
    public bool IsLoaded => !_disposed && _provider.IsReady;

    /// <summary>
    /// Current Z slice index for methods without z parameter.
    /// </summary>
    public long CurrentZ { get; set; }

    /// <summary>
    /// The underlying tile data provider.
    /// </summary>
    public ITileDataProvider Provider => _provider;

    /// <summary>
    /// Create TiledImageSource with a tile data provider.
    /// </summary>
    /// <param name="provider">The tile data provider</param>
    /// <param name="tileSize">Tile size (default 512)</param>
    public TiledImageSource(ITileDataProvider provider, int tileSize = 512)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tileSize = tileSize;
    }

    /// <summary>
    /// Get a tile as ImageTile using CurrentZ.
    /// </summary>
    /// <param name="tileX">Tile X index</param>
    /// <param name="tileY">Tile Y index</param>
    /// <returns>ImageTile with pixel data, or null if out of bounds</returns>
    public ImageTile? GetTile(long tileX, long tileY)
    {
        return GetTile(tileX, tileY, CurrentZ);
    }

    /// <summary>
    /// Get a tile as ImageTile at specified Z slice.
    /// </summary>
    /// <param name="tileX">Tile X index</param>
    /// <param name="tileY">Tile Y index</param>
    /// <param name="z">Z slice index</param>
    /// <returns>ImageTile with pixel data, or null if out of bounds</returns>
    public ImageTile? GetTile(long tileX, long tileY, long z)
    {
        if (_disposed) return null;

        long pixelX = tileX * _tileSize;
        long pixelY = tileY * _tileSize;

        if (pixelX >= ImageWidth || pixelY >= ImageHeight || z < 0 || z >= ImageDepth)
            return null;

        int tileWidth = (int)Math.Min(_tileSize, ImageWidth - pixelX);
        int tileHeight = (int)Math.Min(_tileSize, ImageHeight - pixelY);

        int bufferSize = tileWidth * tileHeight * BytesPerPixel;
        byte[] buffer = new byte[bufferSize];

        _provider.ReadTileData(pixelX, pixelY, z, tileWidth, tileHeight, buffer);

        var tile = new ImageTile(pixelX, pixelY, tileWidth, tileHeight, 0, PixelFormat);
        tile.SetPixelData(buffer);
        return tile;
    }

    /// <summary>
    /// Read pixel data for a specific tile region using CurrentZ.
    /// </summary>
    public byte[]? GetTileData(long tileX, long tileY)
    {
        return GetTileData(tileX, tileY, CurrentZ);
    }

    /// <summary>
    /// Read pixel data for a specific tile region at specified Z slice.
    /// Returns new byte array with tile data.
    /// </summary>
    /// <param name="tileX">Tile X index</param>
    /// <param name="tileY">Tile Y index</param>
    /// <param name="z">Z slice index</param>
    /// <returns>Pixel data for the tile, or null if out of bounds</returns>
    public byte[]? GetTileData(long tileX, long tileY, long z)
    {
        if (_disposed) return null;

        long pixelX = tileX * _tileSize;
        long pixelY = tileY * _tileSize;

        if (pixelX >= ImageWidth || pixelY >= ImageHeight || z < 0 || z >= ImageDepth)
            return null;

        int tileWidth = (int)Math.Min(_tileSize, ImageWidth - pixelX);
        int tileHeight = (int)Math.Min(_tileSize, ImageHeight - pixelY);

        int bufferSize = tileWidth * tileHeight * BytesPerPixel;
        byte[] buffer = new byte[bufferSize];

        _provider.ReadTileData(pixelX, pixelY, z, tileWidth, tileHeight, buffer);
        return buffer;
    }

    /// <summary>
    /// Read pixel data for a specific tile region into provided buffer using CurrentZ.
    /// </summary>
    public bool GetTileData(long tileX, long tileY, byte[] buffer, out int tileWidth, out int tileHeight)
    {
        return GetTileData(tileX, tileY, CurrentZ, buffer, out tileWidth, out tileHeight);
    }

    /// <summary>
    /// Read pixel data for a specific tile region into provided buffer at specified Z slice.
    /// </summary>
    /// <param name="tileX">Tile X index</param>
    /// <param name="tileY">Tile Y index</param>
    /// <param name="z">Z slice index</param>
    /// <param name="buffer">Buffer to write data into</param>
    /// <param name="tileWidth">Output: actual tile width</param>
    /// <param name="tileHeight">Output: actual tile height</param>
    /// <returns>True if successful</returns>
    public bool GetTileData(long tileX, long tileY, long z, byte[] buffer, out int tileWidth, out int tileHeight)
    {
        tileWidth = 0;
        tileHeight = 0;

        if (_disposed) return false;

        long pixelX = tileX * _tileSize;
        long pixelY = tileY * _tileSize;

        if (pixelX >= ImageWidth || pixelY >= ImageHeight || z < 0 || z >= ImageDepth)
            return false;

        tileWidth = (int)Math.Min(_tileSize, ImageWidth - pixelX);
        tileHeight = (int)Math.Min(_tileSize, ImageHeight - pixelY);

        int requiredSize = tileWidth * tileHeight * BytesPerPixel;
        if (buffer.Length < requiredSize)
            return false;

        _provider.ReadTileData(pixelX, pixelY, z, tileWidth, tileHeight, buffer);
        return true;
    }

    /// <summary>
    /// Read raw pixel data from a specific region using CurrentZ.
    /// </summary>
    public void ReadPixels(long x, long y, int width, int height, byte[] buffer)
    {
        ReadPixels(x, y, CurrentZ, width, height, buffer);
    }

    /// <summary>
    /// Read raw pixel data from a specific region at specified Z slice.
    /// </summary>
    public void ReadPixels(long x, long y, long z, int width, int height, byte[] buffer)
    {
        if (_disposed) return;
        _provider.ReadPixels(x, y, z, width, height, buffer);
    }

    /// <summary>
    /// Dispose the image source and its provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _provider.Dispose();
    }
}
