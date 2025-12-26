using TiledImage.Core.Types;

namespace TiledImage.Core.Tiling;

/// <summary>
/// Interface for providing tile data from various image sources.
/// Implementations handle the actual data access (memory-mapped files, raw data, etc.)
/// </summary>
public interface ITileDataProvider : IDisposable
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    long ImageWidth { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    long ImageHeight { get; }

    /// <summary>
    /// Image depth (number of Z slices). 1 for 2D images.
    /// </summary>
    long ImageDepth { get; }

    /// <summary>
    /// Pixel format of the image data.
    /// </summary>
    PixelFormat PixelFormat { get; }

    /// <summary>
    /// Bytes per pixel, derived from PixelFormat.
    /// </summary>
    int BytesPerPixel { get; }

    /// <summary>
    /// Whether the provider is ready to provide data.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Read pixel data for a tile region at specified Z slice.
    /// </summary>
    /// <param name="pixelX">Start X coordinate in pixels</param>
    /// <param name="pixelY">Start Y coordinate in pixels</param>
    /// <param name="z">Z slice index</param>
    /// <param name="tileWidth">Tile width in pixels</param>
    /// <param name="tileHeight">Tile height in pixels</param>
    /// <param name="buffer">Buffer to write data into</param>
    void ReadTileData(long pixelX, long pixelY, long z, int tileWidth, int tileHeight, byte[] buffer);

    /// <summary>
    /// Read raw pixel data from a specific region at specified Z slice.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="z">Z slice index</param>
    /// <param name="width">Width in pixels</param>
    /// <param name="height">Height in pixels</param>
    /// <param name="buffer">Buffer to write data into</param>
    void ReadPixels(long x, long y, long z, int width, int height, byte[] buffer);
}
