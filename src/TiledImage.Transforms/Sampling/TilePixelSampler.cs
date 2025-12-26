using TiledImage.Core.Tiling;

namespace TiledImage.Transforms.Sampling;

/// <summary>
/// IPixelSampler implementation that wraps an ITileDataProvider.
/// Includes tile-based caching for efficient pixel access during resampling.
/// </summary>
public class TilePixelSampler : IPixelSampler
{
    private readonly ITileDataProvider _source;
    private readonly long _z;
    private readonly long _width;
    private readonly long _height;
    private readonly int _bytesPerPixel;

    // Tile cache for efficient pixel access
    private byte[]? _cachedTile;
    private long _cachedTileX = -1;
    private long _cachedTileY = -1;
    private const int CacheTileSize = 64;

    /// <summary>
    /// Create a pixel sampler for the specified Z slice.
    /// </summary>
    /// <param name="source">Source tile data provider</param>
    /// <param name="z">Z slice index (0 for 2D images)</param>
    public TilePixelSampler(ITileDataProvider source, long z = 0)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _z = z;
        _width = source.ImageWidth;
        _height = source.ImageHeight;
        _bytesPerPixel = source.BytesPerPixel;
    }

    /// <inheritdoc/>
    public long Width => _width;

    /// <inheritdoc/>
    public long Height => _height;

    /// <inheritdoc/>
    public int BytesPerPixel => _bytesPerPixel;

    /// <summary>
    /// The Z slice index being sampled.
    /// </summary>
    public long Z => _z;

    /// <inheritdoc/>
    public byte GetGray8(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return 0;

        EnsureCached(x, y);
        int localX = x - (int)(_cachedTileX * CacheTileSize);
        int localY = y - (int)(_cachedTileY * CacheTileSize);
        int tileWidth = (int)Math.Min(CacheTileSize, _width - _cachedTileX * CacheTileSize);
        int offset = (localY * tileWidth + localX) * _bytesPerPixel;

        if (offset >= 0 && offset < _cachedTile!.Length)
            return _cachedTile[offset];
        return 0;
    }

    /// <inheritdoc/>
    public ushort GetGray16(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return 0;

        EnsureCached(x, y);
        int localX = x - (int)(_cachedTileX * CacheTileSize);
        int localY = y - (int)(_cachedTileY * CacheTileSize);
        int tileWidth = (int)Math.Min(CacheTileSize, _width - _cachedTileX * CacheTileSize);
        int offset = (localY * tileWidth + localX) * _bytesPerPixel;

        if (offset >= 0 && offset + 1 < _cachedTile!.Length)
            return (ushort)(_cachedTile[offset] | (_cachedTile[offset + 1] << 8));
        return 0;
    }

    /// <inheritdoc/>
    public void GetRgb24(int x, int y, Span<byte> dest)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            dest[0] = 0;
            dest[1] = 0;
            dest[2] = 0;
            return;
        }

        EnsureCached(x, y);
        int localX = x - (int)(_cachedTileX * CacheTileSize);
        int localY = y - (int)(_cachedTileY * CacheTileSize);
        int tileWidth = (int)Math.Min(CacheTileSize, _width - _cachedTileX * CacheTileSize);
        int offset = (localY * tileWidth + localX) * _bytesPerPixel;

        if (offset >= 0 && offset + 2 < _cachedTile!.Length)
        {
            dest[0] = _cachedTile[offset];
            dest[1] = _cachedTile[offset + 1];
            dest[2] = _cachedTile[offset + 2];
        }
    }

    /// <inheritdoc/>
    public void GetPixelBytes(int x, int y, Span<byte> dest)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            dest.Clear();
            return;
        }

        EnsureCached(x, y);
        int localX = x - (int)(_cachedTileX * CacheTileSize);
        int localY = y - (int)(_cachedTileY * CacheTileSize);
        int tileWidth = (int)Math.Min(CacheTileSize, _width - _cachedTileX * CacheTileSize);
        int offset = (localY * tileWidth + localX) * _bytesPerPixel;

        if (offset >= 0 && offset + _bytesPerPixel <= _cachedTile!.Length)
        {
            _cachedTile.AsSpan(offset, _bytesPerPixel).CopyTo(dest);
        }
    }

    private void EnsureCached(int x, int y)
    {
        long tileX = x / CacheTileSize;
        long tileY = y / CacheTileSize;

        if (tileX != _cachedTileX || tileY != _cachedTileY)
        {
            int tileWidth = (int)Math.Min(CacheTileSize, _width - tileX * CacheTileSize);
            int tileHeight = (int)Math.Min(CacheTileSize, _height - tileY * CacheTileSize);

            if (tileWidth <= 0 || tileHeight <= 0)
            {
                _cachedTile = Array.Empty<byte>();
                _cachedTileX = tileX;
                _cachedTileY = tileY;
                return;
            }

            int bufferSize = tileWidth * tileHeight * _bytesPerPixel;
            if (_cachedTile == null || _cachedTile.Length < bufferSize)
                _cachedTile = new byte[bufferSize];

            _source.ReadPixels(
                tileX * CacheTileSize,
                tileY * CacheTileSize,
                _z,
                tileWidth,
                tileHeight,
                _cachedTile);

            _cachedTileX = tileX;
            _cachedTileY = tileY;
        }
    }
}
