using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Represents a geometric shape in image coordinates.
/// </summary>
public interface IShape
{
    /// <summary>
    /// Unique identifier for this shape.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Center X coordinate in image space.
    /// </summary>
    double CenterX { get; set; }

    /// <summary>
    /// Center Y coordinate in image space.
    /// </summary>
    double CenterY { get; set; }

    /// <summary>
    /// Rotation angle in degrees (clockwise, around center).
    /// </summary>
    double Rotation { get; set; }

    /// <summary>
    /// Visual style for rendering.
    /// </summary>
    ShapeStyle Style { get; set; }

    /// <summary>
    /// Gets the bounding box without rotation applied.
    /// </summary>
    Rect2D GetLocalBounds();

    /// <summary>
    /// Gets the axis-aligned bounding box with rotation applied.
    /// </summary>
    Rect2D GetWorldBounds();

    /// <summary>
    /// Moves the shape by the specified delta.
    /// </summary>
    void Move(double deltaX, double deltaY);
}
