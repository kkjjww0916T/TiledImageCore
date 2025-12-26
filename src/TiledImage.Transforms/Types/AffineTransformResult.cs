using Geometry.Primitives;

namespace TiledImage.Transforms;

/// <summary>
/// Result of affine transform computation.
/// </summary>
/// <param name="Forward">Transform matrix from source to destination coordinates.</param>
/// <param name="Inverse">Inverse transform matrix from destination to source coordinates (for resampling).</param>
/// <param name="OutputWidth">Output image width in pixels.</param>
/// <param name="OutputHeight">Output image height in pixels.</param>
public readonly record struct AffineTransformResult(
    Matrix3x3 Forward,
    Matrix3x3 Inverse,
    long OutputWidth,
    long OutputHeight
);
