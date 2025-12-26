using System.Buffers;
using Geometry.Primitives;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;

namespace TiledImage.Wpf.Viewer.Caching;

/// <summary>
/// Manages MipMap pyramid generation and tile extraction for different zoom levels.
/// </summary>
public class MipMapManager : IDisposable
{
    private readonly TiledImageSource _source;
    private readonly int _tileSize;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private readonly List<MipLevel> _mipLevels = new();
    private const int MaxMipLevels = 8;
    private bool _disposed;

    public MipMapManager(TiledImageSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _tileSize = source.TileSize;
    }

    public int LevelCount => _mipLevels.Count;

    /// <summary>
    /// Initialize MipMap pyramid. Should be called after source is ready.
    /// </summary>
    public void Initialize()
    {
        _mipLevels.Clear();

        // Level 0 references the original source (no data copy)
        _mipLevels.Add(new MipLevel
        {
            Level = 0,
            Width = _source.ImageWidth,
            Height = _source.ImageHeight,
            Data = null, // Level 0 reads directly from source
            TilesX = _source.TilesX,
            TilesY = _source.TilesY
        });

        // Generate downsampled levels
        GenerateMipLevels();
    }

    /// <summary>
    /// Get the optimal mip level for the given zoom factor.
    /// </summary>
    public int GetOptimalMipLevel(double zoom)
    {
        if (_mipLevels.Count == 0) return 0;
        if (zoom >= 1.0) return 0;

        // Find the mip level where each source pixel maps to ~1 screen pixel
        int level = (int)Math.Floor(-Math.Log2(zoom));
        return Math.Clamp(level, 0, _mipLevels.Count - 1);
    }

    /// <summary>
    /// Get MipLevel info for a specific level.
    /// </summary>
    public MipLevel? GetLevel(int level)
    {
        if (level < 0 || level >= _mipLevels.Count)
            return null;
        return _mipLevels[level];
    }

    /// <summary>
    /// Get tile data for the specified tile at given mip level.
    /// </summary>
    public ImageTile? GetTile(long tileX, long tileY, int mipLevel)
    {
        if (_disposed || _mipLevels.Count == 0) return null;

        mipLevel = Math.Clamp(mipLevel, 0, _mipLevels.Count - 1);
        var mip = _mipLevels[mipLevel];

        // Calculate pixel coordinates at this mip level
        long pixelX = tileX * _tileSize;
        long pixelY = tileY * _tileSize;

        // Check bounds
        if (pixelX >= mip.Width || pixelY >= mip.Height)
            return null;

        // Calculate actual tile size (may be smaller at edges)
        int tileWidth = (int)Math.Min(_tileSize, mip.Width - pixelX);
        int tileHeight = (int)Math.Min(_tileSize, mip.Height - pixelY);

        // Create tile with original image coordinates and correct format
        int scale = 1 << mipLevel;
        var tile = new ImageTile(pixelX * scale, pixelY * scale, tileWidth, tileHeight, mipLevel, _source.PixelFormat);

        // Load pixel data
        int bufferSize = tileWidth * tileHeight * _source.BytesPerPixel;
        byte[] pixelData = _arrayPool.Rent(bufferSize);

        try
        {
            ExtractTileData(mip, pixelX, pixelY, tileWidth, tileHeight, pixelData);
            tile.SetPixelData(pixelData, bufferSize, _arrayPool);
        }
        catch
        {
            _arrayPool.Return(pixelData);
            throw;
        }

        return tile;
    }

    /// <summary>
    /// Get all tiles that intersect with the given viewport rectangle.
    /// </summary>
    public IEnumerable<ImageTile> GetTilesInViewport(Rect2L viewport, double zoom)
    {
        if (_disposed || _mipLevels.Count == 0)
            yield break;

        int mipLevel = GetOptimalMipLevel(zoom);
        var mip = _mipLevels[mipLevel];
        int scale = 1 << mipLevel;

        // Convert viewport to mip level coordinates
        long mipViewportX = viewport.X / scale;
        long mipViewportY = viewport.Y / scale;
        long mipViewportRight = (viewport.Right + scale - 1) / scale;
        long mipViewportBottom = (viewport.Bottom + scale - 1) / scale;

        // Calculate tile range at this mip level
        long startTileX = Math.Max(0, mipViewportX / _tileSize);
        long startTileY = Math.Max(0, mipViewportY / _tileSize);
        long endTileX = Math.Min(mip.TilesX - 1, (mipViewportRight - 1) / _tileSize);
        long endTileY = Math.Min(mip.TilesY - 1, (mipViewportBottom - 1) / _tileSize);

        for (long ty = startTileY; ty <= endTileY; ty++)
        {
            for (long tx = startTileX; tx <= endTileX; tx++)
            {
                var tile = GetTile(tx, ty, mipLevel);
                if (tile != null)
                {
                    yield return tile;
                }
            }
        }
    }

    private void GenerateMipLevels()
    {
        // First, read level 0 data from source for generating level 1
        long currentWidth = _source.ImageWidth;
        long currentHeight = _source.ImageHeight;

        // Generate level 1 from source
        if (currentWidth > _tileSize || currentHeight > _tileSize)
        {
            long newWidth = currentWidth / 2;
            long newHeight = currentHeight / 2;

            if (newWidth >= _tileSize || newHeight >= _tileSize)
            {
                byte[] mip1Data = GenerateLevel1FromSource(currentWidth, currentHeight, newWidth, newHeight);

                _mipLevels.Add(new MipLevel
                {
                    Level = 1,
                    Width = newWidth,
                    Height = newHeight,
                    Data = mip1Data,
                    TilesX = (int)Math.Ceiling((double)newWidth / _tileSize),
                    TilesY = (int)Math.Ceiling((double)newHeight / _tileSize)
                });

                currentWidth = newWidth;
                currentHeight = newHeight;
                byte[] currentData = mip1Data;

                // Generate subsequent levels from previous level data
                for (int level = 2; level < MaxMipLevels; level++)
                {
                    newWidth = currentWidth / 2;
                    newHeight = currentHeight / 2;

                    if (newWidth < _tileSize && newHeight < _tileSize)
                        break;

                    byte[] newData = DownsampleLevel(currentData, currentWidth, currentHeight, newWidth, newHeight);

                    _mipLevels.Add(new MipLevel
                    {
                        Level = level,
                        Width = newWidth,
                        Height = newHeight,
                        Data = newData,
                        TilesX = (int)Math.Ceiling((double)newWidth / _tileSize),
                        TilesY = (int)Math.Ceiling((double)newHeight / _tileSize)
                    });

                    currentWidth = newWidth;
                    currentHeight = newHeight;
                    currentData = newData;
                }
            }
        }
    }

    private byte[] GenerateLevel1FromSource(long srcWidth, long srcHeight, long dstWidth, long dstHeight)
    {
        var format = _source.PixelFormat;
        int bytesPerPixel = format.GetBytesPerPixel();
        byte[] result = new byte[dstWidth * dstHeight * bytesPerPixel];

        // Process row by row to minimize memory usage
        Parallel.For(0, (int)dstHeight, y =>
        {
            byte[] row0 = new byte[srcWidth * bytesPerPixel];
            byte[] row1 = new byte[srcWidth * bytesPerPixel];

            long srcY = y * 2;
            _source.ReadPixels(0, srcY, (int)srcWidth, 1, row0);
            if (srcY + 1 < srcHeight)
            {
                _source.ReadPixels(0, srcY + 1, (int)srcWidth, 1, row1);
            }
            else
            {
                Array.Copy(row0, row1, row0.Length);
            }

            for (int x = 0; x < dstWidth; x++)
            {
                int srcX = x * 2;
                long destOffset = (y * dstWidth + x) * bytesPerPixel;

                // Sample 2x2 block - average each channel
                DownsamplePixel2x2(row0, row1, srcX, (int)srcWidth, bytesPerPixel, format, result, destOffset);
            }
        });

        return result;
    }

    private byte[] DownsampleLevel(byte[] srcData, long srcWidth, long srcHeight, long dstWidth, long dstHeight)
    {
        var format = _source.PixelFormat;
        int bytesPerPixel = format.GetBytesPerPixel();
        byte[] result = new byte[dstWidth * dstHeight * bytesPerPixel];

        Parallel.For(0, (int)dstHeight, y =>
        {
            for (int x = 0; x < dstWidth; x++)
            {
                long srcX = x * 2;
                long srcY = y * 2;
                long destOffset = (y * dstWidth + x) * bytesPerPixel;

                DownsamplePixel2x2FromData(srcData, srcWidth, srcHeight, srcX, srcY,
                    bytesPerPixel, format, result, destOffset);
            }
        });

        return result;
    }

    /// <summary>
    /// Downsample a 2x2 pixel block from source data array.
    /// </summary>
    private static void DownsamplePixel2x2FromData(
        byte[] srcData, long srcWidth, long srcHeight,
        long srcX, long srcY,
        int bytesPerPixel, PixelFormat format,
        byte[] dest, long destOffset)
    {
        if (format == PixelFormat.Gray16)
        {
            // Gray16: average 16-bit values
            int sum = 0;
            int sampleCount = 0;

            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    long sx = Math.Min(srcX + dx, srcWidth - 1);
                    long sy = Math.Min(srcY + dy, srcHeight - 1);
                    long srcOffset = (sy * srcWidth + sx) * 2;

                    if (srcOffset >= 0 && srcOffset + 2 <= srcData.Length)
                    {
                        ushort value = (ushort)(srcData[srcOffset] | (srcData[srcOffset + 1] << 8));
                        sum += value;
                        sampleCount++;
                    }
                }
            }

            if (sampleCount > 0)
            {
                ushort avg = (ushort)(sum / sampleCount);
                dest[destOffset] = (byte)(avg & 0xFF);
                dest[destOffset + 1] = (byte)(avg >> 8);
            }
        }
        else
        {
            // Other formats: average each byte channel
            int[] channelSums = new int[bytesPerPixel];
            int sampleCount = 0;

            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    long sx = Math.Min(srcX + dx, srcWidth - 1);
                    long sy = Math.Min(srcY + dy, srcHeight - 1);
                    long srcOffset = (sy * srcWidth + sx) * bytesPerPixel;

                    if (srcOffset >= 0 && srcOffset + bytesPerPixel <= srcData.Length)
                    {
                        for (int c = 0; c < bytesPerPixel; c++)
                        {
                            channelSums[c] += srcData[srcOffset + c];
                        }
                        sampleCount++;
                    }
                }
            }

            if (sampleCount > 0)
            {
                for (int c = 0; c < bytesPerPixel; c++)
                {
                    dest[destOffset + c] = (byte)(channelSums[c] / sampleCount);
                }
            }
        }
    }

    /// <summary>
    /// Downsample a 2x2 pixel block from two rows.
    /// </summary>
    private static void DownsamplePixel2x2(
        byte[] row0, byte[] row1,
        int srcX, int srcWidth,
        int bytesPerPixel, PixelFormat format,
        byte[] dest, long destOffset)
    {
        if (format == PixelFormat.Gray16)
        {
            // Gray16: average 16-bit values
            int sum = 0;
            int sampleCount = 0;

            for (int dx = 0; dx < 2; dx++)
            {
                int sx = Math.Min(srcX + dx, srcWidth - 1);
                int offset = sx * 2;

                if (offset + 2 <= row0.Length)
                {
                    ushort v0 = (ushort)(row0[offset] | (row0[offset + 1] << 8));
                    ushort v1 = (ushort)(row1[offset] | (row1[offset + 1] << 8));
                    sum += v0 + v1;
                    sampleCount += 2;
                }
            }

            if (sampleCount > 0)
            {
                ushort avg = (ushort)(sum / sampleCount);
                dest[destOffset] = (byte)(avg & 0xFF);
                dest[destOffset + 1] = (byte)(avg >> 8);
            }
        }
        else
        {
            // Other formats: average each byte channel
            int[] channelSums = new int[bytesPerPixel];
            int sampleCount = 0;

            for (int dx = 0; dx < 2; dx++)
            {
                int sx = Math.Min(srcX + dx, srcWidth - 1);
                int offset = sx * bytesPerPixel;

                if (offset + bytesPerPixel <= row0.Length)
                {
                    for (int c = 0; c < bytesPerPixel; c++)
                    {
                        channelSums[c] += row0[offset + c];
                        channelSums[c] += row1[offset + c];
                    }
                    sampleCount += 2;
                }
            }

            if (sampleCount > 0)
            {
                for (int c = 0; c < bytesPerPixel; c++)
                {
                    dest[destOffset + c] = (byte)(channelSums[c] / sampleCount);
                }
            }
        }
    }

    private void ExtractTileData(MipLevel mip, long pixelX, long pixelY, int tileWidth, int tileHeight, byte[] destBuffer)
    {
        int bytesPerPixel = _source.BytesPerPixel;

        if (mip.Level == 0)
        {
            // Read directly from source
            _source.ReadPixels(pixelX, pixelY, tileWidth, tileHeight, destBuffer);
        }
        else if (mip.Data != null)
        {
            // Copy from mip level data
            ReadOnlySpan<byte> source = mip.Data.AsSpan();
            Span<byte> dest = destBuffer.AsSpan();
            int rowBytes = tileWidth * bytesPerPixel;

            for (int y = 0; y < tileHeight; y++)
            {
                long sourceOffset = ((pixelY + y) * mip.Width + pixelX) * bytesPerPixel;
                int destOffset = y * rowBytes;

                if (sourceOffset >= 0 && sourceOffset + rowBytes <= source.Length)
                {
                    source.Slice((int)sourceOffset, rowBytes).CopyTo(dest.Slice(destOffset, rowBytes));
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var mip in _mipLevels)
        {
            mip.Dispose();
        }
        _mipLevels.Clear();
    }
}

/// <summary>
/// Represents a mip level in the image pyramid.
/// </summary>
public class MipLevel : IDisposable
{
    public int Level { get; init; }
    public long Width { get; init; }
    public long Height { get; init; }
    public byte[]? Data { get; set; }
    public int TilesX { get; init; }
    public int TilesY { get; init; }

    public void Dispose()
    {
        // Clear reference to allow GC
        if (Level > 0) // Don't clear level 0 as it references source
        {
            Data = null;
        }
    }
}
