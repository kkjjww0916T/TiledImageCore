using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;

namespace TiledImage.Core.IO;

/// <summary>
/// Interface for loading images from files into TiledImageSource.
/// Handles the full loading pipeline: file access, buffer creation, format parsing, and source creation.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Gets or sets the buffer type to use when loading images.
    /// </summary>
    BufferType BufferType { get; set; }

    /// <summary>
    /// Load image from file path and return TiledImageSource.
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="tileSize">Tile size for the resulting TiledImageSource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TiledImageSource ready for use with control</returns>
    Task<TiledImageSource> LoadAsync(string filePath, int tileSize = 512, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this loader can handle the specified file.
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <returns>True if this loader can load the file</returns>
    bool CanLoad(string filePath);
}
