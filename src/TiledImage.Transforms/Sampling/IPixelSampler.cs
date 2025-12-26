namespace TiledImage.Transforms.Sampling;

/// <summary>
/// Abstraction for pixel access during image resampling.
/// Allows decoupling of interpolation algorithms from specific image source implementations.
/// </summary>
public interface IPixelSampler
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    long Width { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    long Height { get; }

    /// <summary>
    /// Bytes per pixel.
    /// </summary>
    int BytesPerPixel { get; }

    /// <summary>
    /// Get a single Gray8 pixel value at the specified coordinates.
    /// Returns 0 for out-of-bounds coordinates.
    /// </summary>
    byte GetGray8(int x, int y);

    /// <summary>
    /// Get a single Gray16 pixel value at the specified coordinates.
    /// Returns 0 for out-of-bounds coordinates.
    /// </summary>
    ushort GetGray16(int x, int y);

    /// <summary>
    /// Get RGB24 pixel values at the specified coordinates.
    /// Writes 3 bytes to the destination span in RGB order.
    /// Writes zeros for out-of-bounds coordinates.
    /// </summary>
    void GetRgb24(int x, int y, Span<byte> dest);

    /// <summary>
    /// Get raw pixel bytes at the specified coordinates.
    /// Writes BytesPerPixel bytes to the destination span.
    /// Writes zeros for out-of-bounds coordinates.
    /// </summary>
    void GetPixelBytes(int x, int y, Span<byte> dest);
}
