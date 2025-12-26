using TiledImage.Core.Tiling;
using TiledImage.Core.Types;

namespace TiledImage.Core.IO;

/// <summary>
/// Interface for exporting TiledImageSource to image files.
/// Handles encoding and saving without any transformation logic.
/// </summary>
public interface IImageExporter
{
    /// <summary>
    /// Export image source to file.
    /// </summary>
    /// <param name="source">Source image data</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="format">Output format</param>
    /// <param name="quality">Quality (0-100, used for JPEG/WebP)</param>
    Task ExportAsync(TiledImageSource source, string filePath, ImageFormat format, int quality = 95);
}
