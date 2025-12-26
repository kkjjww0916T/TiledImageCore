namespace TiledImage.Transforms;

/// <summary>
/// Interpolation mode for pixel resampling during image transformation.
/// </summary>
public enum InterpolationMode
{
    /// <summary>
    /// Nearest neighbor interpolation. Fastest, but produces blocky results.
    /// </summary>
    NearestNeighbor,

    /// <summary>
    /// Bilinear interpolation. Good balance between speed and quality.
    /// </summary>
    Bilinear,

    /// <summary>
    /// Bicubic interpolation. Highest quality, but slower.
    /// </summary>
    Bicubic
}
