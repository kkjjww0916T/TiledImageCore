namespace TiledImage.Core.Types;

/// <summary>
/// Platform-independent image encoding format.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Gif
}

/// <summary>
/// Platform-independent pixel format.
/// </summary>
public enum PixelFormat
{
    /// <summary>
    /// 32-bit BGRA (Blue, Green, Red, Alpha) - standard Windows format.
    /// </summary>
    Bgra32,

    /// <summary>
    /// 32-bit RGBA (Red, Green, Blue, Alpha).
    /// </summary>
    Rgba32,

    /// <summary>
    /// 24-bit BGR (Blue, Green, Red) without alpha.
    /// </summary>
    Bgr24,

    /// <summary>
    /// 24-bit RGB (Red, Green, Blue) without alpha.
    /// </summary>
    Rgb24,

    /// <summary>
    /// 8-bit grayscale.
    /// </summary>
    Gray8,

    /// <summary>
    /// 16-bit grayscale.
    /// </summary>
    Gray16
}

/// <summary>
/// Extension methods for pixel format.
/// </summary>
public static class PixelFormatExtensions
{
    /// <summary>
    /// Get bytes per pixel for the format.
    /// </summary>
    public static int GetBytesPerPixel(this PixelFormat format) => format switch
    {
        PixelFormat.Bgra32 => 4,
        PixelFormat.Rgba32 => 4,
        PixelFormat.Bgr24 => 3,
        PixelFormat.Rgb24 => 3,
        PixelFormat.Gray8 => 1,
        PixelFormat.Gray16 => 2,
        _ => 4
    };

    /// <summary>
    /// Check if the format has an alpha channel.
    /// </summary>
    public static bool HasAlpha(this PixelFormat format) => format switch
    {
        PixelFormat.Bgra32 => true,
        PixelFormat.Rgba32 => true,
        _ => false
    };

    /// <summary>
    /// Get channel count for the format.
    /// </summary>
    public static int GetChannelCount(this PixelFormat format) => format switch
    {
        PixelFormat.Bgra32 => 4,
        PixelFormat.Rgba32 => 4,
        PixelFormat.Bgr24 => 3,
        PixelFormat.Rgb24 => 3,
        PixelFormat.Gray8 => 1,
        PixelFormat.Gray16 => 1,
        _ => 4
    };

    /// <summary>
    /// Get channel offsets for the format (B, G, R, A order).
    /// Returns (-1) for missing channels.
    /// </summary>
    public static (int Blue, int Green, int Red, int Alpha) GetChannelOffsets(this PixelFormat format) => format switch
    {
        PixelFormat.Bgra32 => (0, 1, 2, 3),
        PixelFormat.Rgba32 => (2, 1, 0, 3),
        PixelFormat.Bgr24 => (0, 1, 2, -1),
        PixelFormat.Rgb24 => (2, 1, 0, -1),
        PixelFormat.Gray8 => (0, 0, 0, -1),
        PixelFormat.Gray16 => (0, 0, 0, -1), // 16-bit gray uses 2 bytes per pixel
        _ => (0, 1, 2, 3)
    };

    /// <summary>
    /// Check if the format is grayscale.
    /// </summary>
    public static bool IsGrayscale(this PixelFormat format) => format == PixelFormat.Gray8 || format == PixelFormat.Gray16;

    /// <summary>
    /// Get bits per pixel for the format.
    /// </summary>
    public static int GetBitsPerPixel(this PixelFormat format) => format switch
    {
        PixelFormat.Bgra32 => 32,
        PixelFormat.Rgba32 => 32,
        PixelFormat.Bgr24 => 24,
        PixelFormat.Rgb24 => 24,
        PixelFormat.Gray8 => 8,
        PixelFormat.Gray16 => 16,
        _ => 32
    };
}
