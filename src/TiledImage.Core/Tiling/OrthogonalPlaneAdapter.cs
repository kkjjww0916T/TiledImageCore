using System.Collections.Concurrent;
using TiledImage.Core.Types;

namespace TiledImage.Core.Tiling;

/// <summary>
/// Adapter that wraps an ITileDataProvider to provide access to XZ and YZ planes.
/// For XY plane, it passes through to the source provider directly.
/// For XZ/YZ planes, it reconstructs tile data by reading from multiple Z slices.
/// </summary>
public class OrthogonalPlaneAdapter : ITileDataProvider
{
    private readonly ITileDataProvider _source;
    private readonly OrthogonalPlane _plane;
    private readonly long _planeWidth;
    private readonly long _planeHeight;
    private readonly long _planeDepth;

    // Row cache for XZ/YZ plane reconstruction
    private readonly ConcurrentDictionary<(long Z, long Slice), byte[]> _rowCache;
    private readonly int _maxCacheEntries;
    private readonly object _cacheLock = new();
    private long _currentSlice;
    private bool _disposed;

    /// <summary>
    /// Creates a new orthogonal plane adapter.
    /// </summary>
    /// <param name="source">The source provider (XY-based).</param>
    /// <param name="plane">The target viewing plane.</param>
    /// <param name="maxCacheEntries">Maximum number of cached rows (default: 500 for multi-slice caching).</param>
    public OrthogonalPlaneAdapter(ITileDataProvider source, OrthogonalPlane plane, int maxCacheEntries = 500)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _plane = plane;
        _maxCacheEntries = maxCacheEntries;
        _rowCache = new ConcurrentDictionary<(long, long), byte[]>();

        // Calculate plane dimensions
        var dims = plane.GetPlaneDimensions(source.ImageWidth, source.ImageHeight, source.ImageDepth);
        _planeWidth = dims.PlaneWidth;
        _planeHeight = dims.PlaneHeight;
        _planeDepth = dims.PlaneDepth;
    }

    /// <summary>
    /// The viewing plane this adapter provides.
    /// </summary>
    public OrthogonalPlane ViewPlane => _plane;

    /// <summary>
    /// Width of the plane (horizontal axis).
    /// </summary>
    public long ImageWidth => _planeWidth;

    /// <summary>
    /// Height of the plane (vertical axis).
    /// </summary>
    public long ImageHeight => _planeHeight;

    /// <summary>
    /// Depth of the plane (number of slices perpendicular to this plane).
    /// </summary>
    public long ImageDepth => _planeDepth;

    /// <summary>
    /// Pixel format from the source provider.
    /// </summary>
    public PixelFormat PixelFormat => _source.PixelFormat;

    /// <summary>
    /// Bytes per pixel from the source provider.
    /// </summary>
    public int BytesPerPixel => _source.BytesPerPixel;

    /// <summary>
    /// Whether the adapter is ready (source is ready).
    /// </summary>
    public bool IsReady => _source.IsReady;

    /// <summary>
    /// Gets or sets the current slice index.
    /// Cache is preserved on slice change for faster navigation between adjacent slices.
    /// </summary>
    public long CurrentSlice
    {
        get => _currentSlice;
        set => _currentSlice = value;
    }

    /// <summary>
    /// Read tile data for a region at the specified slice.
    /// </summary>
    /// <param name="pixelX">Start U coordinate in plane (horizontal).</param>
    /// <param name="pixelY">Start V coordinate in plane (vertical).</param>
    /// <param name="slice">Slice index (depth).</param>
    /// <param name="tileWidth">Tile width in pixels.</param>
    /// <param name="tileHeight">Tile height in pixels.</param>
    /// <param name="buffer">Buffer to write data into.</param>
    public void ReadTileData(long pixelX, long pixelY, long slice, int tileWidth, int tileHeight, byte[] buffer)
    {
        if (_plane == OrthogonalPlane.XY)
        {
            // Direct pass-through for XY plane
            _source.ReadTileData(pixelX, pixelY, slice, tileWidth, tileHeight, buffer);
            return;
        }

        // For XZ/YZ planes, reconstruct from multiple Z slices
        ReadOrthogonalTileData(pixelX, pixelY, slice, tileWidth, tileHeight, buffer);
    }

    /// <summary>
    /// Read pixel data from a region at the specified slice.
    /// </summary>
    public void ReadPixels(long x, long y, long slice, int width, int height, byte[] buffer)
    {
        if (_plane == OrthogonalPlane.XY)
        {
            // Direct pass-through for XY plane
            _source.ReadPixels(x, y, slice, width, height, buffer);
            return;
        }

        // For XZ/YZ planes, reconstruct from multiple Z slices
        ReadOrthogonalPixels(x, y, slice, width, height, buffer);
    }

    /// <summary>
    /// Clear the row cache.
    /// </summary>
    public void ClearCache()
    {
        _rowCache.Clear();
    }

    /// <summary>
    /// Reconstructs tile data for XZ or YZ planes by reading from multiple Z slices.
    /// </summary>
    private void ReadOrthogonalTileData(long pixelU, long pixelV, long slice, int tileWidth, int tileHeight, byte[] buffer)
    {
        int bytesPerPixel = BytesPerPixel;
        int rowBytes = tileWidth * bytesPerPixel;

        // Clamp to valid bounds
        int actualWidth = (int)Math.Min(tileWidth, _planeWidth - pixelU);
        int actualHeight = (int)Math.Min(tileHeight, _planeHeight - pixelV);

        if (actualWidth <= 0 || actualHeight <= 0)
        {
            Array.Clear(buffer, 0, buffer.Length);
            return;
        }

        // Process each row (vertical axis)
        for (int v = 0; v < actualHeight; v++)
        {
            long planeV = pixelV + v;
            int bufferRowOffset = v * rowBytes;

            // Convert plane coordinates to volume coordinates
            var (x, y, z) = _plane.ToVolumeCoordinates(pixelU, planeV, slice);

            // Get row identifier based on plane type
            // - XZ: rows are identified by Z position (V=Z)
            // - YZ: rows are identified by Y position (V=Y)
            long rowId = _plane == OrthogonalPlane.XZ ? z : y;

            // Try to get cached row data
            byte[]? rowData = GetOrReadRow(rowId, slice, planeV);
            if (rowData == null)
            {
                // Fill with zeros if reading failed
                Array.Clear(buffer, bufferRowOffset, rowBytes);
                continue;
            }

            // Copy the relevant portion of the row
            // For XZ: pixelU is X coordinate, row contains X values
            // For YZ: pixelU is Z coordinate, row contains Z values
            int sourceOffset = GetRowPixelOffset(pixelU) * bytesPerPixel;
            int copyBytes = actualWidth * bytesPerPixel;

            if (sourceOffset + copyBytes <= rowData.Length)
            {
                Buffer.BlockCopy(rowData, sourceOffset, buffer, bufferRowOffset, copyBytes);
            }

            // Clear remaining pixels if tile extends beyond image bounds
            if (actualWidth < tileWidth)
            {
                Array.Clear(buffer, bufferRowOffset + copyBytes, (tileWidth - actualWidth) * bytesPerPixel);
            }
        }

        // Clear remaining rows if tile extends beyond image bounds
        if (actualHeight < tileHeight)
        {
            int clearStart = actualHeight * rowBytes;
            Array.Clear(buffer, clearStart, buffer.Length - clearStart);
        }
    }

    /// <summary>
    /// Reconstructs pixel data for XZ or YZ planes.
    /// </summary>
    private void ReadOrthogonalPixels(long u, long v, long slice, int width, int height, byte[] buffer)
    {
        int bytesPerPixel = BytesPerPixel;
        int rowBytes = width * bytesPerPixel;

        for (int row = 0; row < height; row++)
        {
            long planeV = v + row;
            if (planeV < 0 || planeV >= _planeHeight)
            {
                Array.Clear(buffer, row * rowBytes, rowBytes);
                continue;
            }

            var (x, y, z) = _plane.ToVolumeCoordinates(u, planeV, slice);

            // Get row identifier based on plane type
            // - XZ: rows are identified by Z position (V=Z)
            // - YZ: rows are identified by Y position (V=Y)
            long rowId = _plane == OrthogonalPlane.XZ ? z : y;

            byte[]? rowData = GetOrReadRow(rowId, slice, planeV);
            if (rowData == null)
            {
                Array.Clear(buffer, row * rowBytes, rowBytes);
                continue;
            }

            int sourceOffset = GetRowPixelOffset(u) * bytesPerPixel;
            int copyBytes = Math.Min(width * bytesPerPixel, rowData.Length - sourceOffset);

            if (copyBytes > 0)
            {
                Buffer.BlockCopy(rowData, sourceOffset, buffer, row * rowBytes, copyBytes);
            }

            if (copyBytes < rowBytes)
            {
                Array.Clear(buffer, row * rowBytes + copyBytes, rowBytes - copyBytes);
            }
        }
    }

    /// <summary>
    /// Get row data from cache or read from source.
    /// </summary>
    private byte[]? GetOrReadRow(long z, long slice, long planeV)
    {
        var cacheKey = (z, slice);

        if (_rowCache.TryGetValue(cacheKey, out byte[]? cached))
        {
            return cached;
        }

        // Need to read the row from source
        byte[] rowData = ReadRowFromSource(z, slice);

        // Add to cache with LRU eviction
        lock (_cacheLock)
        {
            if (_rowCache.Count >= _maxCacheEntries)
            {
                // Simple eviction: clear half the cache when full
                var keysToRemove = _rowCache.Keys.Take(_maxCacheEntries / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _rowCache.TryRemove(key, out _);
                }
            }
            _rowCache.TryAdd(cacheKey, rowData);
        }

        return rowData;
    }

    /// <summary>
    /// Read a complete row from the source provider.
    /// </summary>
    /// <param name="rowId">Row identifier: Z for XZ plane, Y for YZ plane</param>
    /// <param name="slice">Slice position: Y for XZ plane, X for YZ plane</param>
    private byte[] ReadRowFromSource(long rowId, long slice)
    {
        int rowWidth;
        byte[] rowData;

        switch (_plane)
        {
            case OrthogonalPlane.XZ:
                // XZ plane: U=X (horizontal), V=Z (vertical), Slice=Y
                // For a row at vertical position V=z (rowId), read along X axis
                // Source: x varies, y=slice, z=rowId
                rowWidth = (int)_source.ImageWidth;
                rowData = new byte[rowWidth * BytesPerPixel];
                _source.ReadPixels(0, slice, rowId, rowWidth, 1, rowData);
                break;

            case OrthogonalPlane.YZ:
                // YZ plane: U=Z (horizontal), V=Y (vertical), Slice=X
                // For a row at vertical position V=y (rowId), read along Z axis
                // Source: x=slice, y=rowId, z varies
                rowWidth = (int)_source.ImageDepth;
                rowData = new byte[rowWidth * BytesPerPixel];
                ReadDepthLine(slice, rowId, rowData);
                break;

            default:
                rowData = Array.Empty<byte>();
                break;
        }

        return rowData;
    }

    /// <summary>
    /// Read a line along Z axis (depth) from the source.
    /// For YZ plane, we need to read pixels along the Z axis at fixed X and Y.
    /// </summary>
    /// <param name="x">Fixed X position (slice for YZ plane)</param>
    /// <param name="y">Fixed Y position (row identifier for YZ plane)</param>
    /// <param name="buffer">Output buffer for the Z line</param>
    private void ReadDepthLine(long x, long y, byte[] buffer)
    {
        int bytesPerPixel = BytesPerPixel;
        int depth = (int)_source.ImageDepth;
        int width = (int)_source.ImageWidth;

        // Optimization: Read entire XY slices in chunks and extract the pixel at (x, y)
        const int ChunkDepth = 64;

        for (int startZ = 0; startZ < depth; startZ += ChunkDepth)
        {
            int chunkSlices = Math.Min(ChunkDepth, depth - startZ);

            // For each Z slice in the chunk, read the row containing (x, y) and extract the pixel
            byte[] rowBuffer = new byte[width * bytesPerPixel];

            for (int dz = 0; dz < chunkSlices; dz++)
            {
                int z = startZ + dz;
                // Read the row at Y=y, Z=z
                _source.ReadPixels(0, y, z, width, 1, rowBuffer);

                // Extract pixel at X=x
                int sourceOffset = (int)x * bytesPerPixel;
                int destOffset = z * bytesPerPixel;
                Buffer.BlockCopy(rowBuffer, sourceOffset, buffer, destOffset, bytesPerPixel);
            }
        }
    }

    /// <summary>
    /// Get the pixel offset in a cached row based on U coordinate.
    /// </summary>
    private int GetRowPixelOffset(long u)
    {
        return (int)Math.Max(0, u);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ClearCache();
            // Note: We don't dispose _source as we don't own it
        }
    }
}
