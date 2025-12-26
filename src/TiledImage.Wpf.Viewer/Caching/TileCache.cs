using System.Collections.Concurrent;
using TiledImage.Core.Tiling;

namespace TiledImage.Wpf.Viewer.Caching;

/// <summary>
/// Cache for image tiles with LRU eviction policy.
/// Manages tiles across different MipMap levels.
/// </summary>
public class TileCache : IDisposable
{
    private readonly int _maxCachedTiles;
    private readonly ConcurrentDictionary<(long TileX, long TileY, int MipLevel), ImageTile> _cache;
    private readonly object _evictionLock = new();
    private bool _disposed;

    public TileCache(int maxCachedTiles = 100)
    {
        _maxCachedTiles = maxCachedTiles;
        _cache = new ConcurrentDictionary<(long, long, int), ImageTile>();
    }

    public int Count => _cache.Count;

    /// <summary>
    /// Try to get a cached tile.
    /// </summary>
    public bool TryGet(long tileX, long tileY, int mipLevel, out ImageTile? tile)
    {
        if (_cache.TryGetValue((tileX, tileY, mipLevel), out tile))
        {
            tile.UpdateLastAccessed();
            return true;
        }
        tile = null;
        return false;
    }

    /// <summary>
    /// Add a tile to the cache.
    /// </summary>
    public void Add(long tileX, long tileY, int mipLevel, ImageTile tile)
    {
        if (_disposed) return;

        // Evict if necessary
        if (_cache.Count >= _maxCachedTiles)
        {
            EvictOldestTiles();
        }

        _cache[(tileX, tileY, mipLevel)] = tile;
    }

    /// <summary>
    /// Clear all cached tiles.
    /// </summary>
    public void Clear()
    {
        foreach (var tile in _cache.Values)
        {
            tile.Dispose();
        }
        _cache.Clear();
    }

    /// <summary>
    /// Clear tiles for a specific MipMap level.
    /// </summary>
    public void ClearLevel(int mipLevel)
    {
        var keysToRemove = _cache.Keys.Where(k => k.MipLevel == mipLevel).ToList();
        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var tile))
            {
                tile.Dispose();
            }
        }
    }

    private void EvictOldestTiles()
    {
        lock (_evictionLock)
        {
            if (_cache.Count < _maxCachedTiles) return;

            // Remove oldest 25% of tiles
            int tilesToRemove = Math.Max(1, _maxCachedTiles / 4);
            var oldestTiles = _cache
                .OrderBy(kv => kv.Value.LastAccessed)
                .Take(tilesToRemove)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldestTiles)
            {
                if (_cache.TryRemove(key, out var tile))
                {
                    tile.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
