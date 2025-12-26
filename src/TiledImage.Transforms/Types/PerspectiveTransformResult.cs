using Geometry.Primitives;

namespace TiledImage.Transforms;

/// <summary>
/// Result of perspective transform computation.
/// </summary>
/// <param name="Forward">Transform matrix from source to destination coordinates.</param>
/// <param name="Inverse">Inverse transform matrix from destination to source coordinates (for resampling).</param>
/// <param name="SourceCorners">Source quadrilateral corners (TopLeft, TopRight, BottomRight, BottomLeft).</param>
/// <param name="DestinationCorners">Destination quadrilateral corners (TopLeft, TopRight, BottomRight, BottomLeft).</param>
public readonly record struct PerspectiveTransformResult(
    Matrix3x3 Forward,
    Matrix3x3 Inverse,
    Point2F[] SourceCorners,
    Point2F[] DestinationCorners
);
