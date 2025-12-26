using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Represents an ellipse or circle shape.
/// A circle is an ellipse where RadiusX == RadiusY.
/// </summary>
public class EllipseShape : ShapeBase
{
    private double _radiusX;
    public double RadiusX
    {
        get => _radiusX;
        set => SetProperty(ref _radiusX, Math.Max(0, value));
    }

    private double _radiusY;
    public double RadiusY
    {
        get => _radiusY;
        set => SetProperty(ref _radiusY, Math.Max(0, value));
    }

    /// <summary>
    /// Creates an ellipse with the specified radii.
    /// </summary>
    public EllipseShape(double centerX, double centerY, double radiusX, double radiusY)
    {
        CenterX = centerX;
        CenterY = centerY;
        RadiusX = radiusX;
        RadiusY = radiusY;
    }

    /// <summary>
    /// Creates a circle with the specified radius.
    /// </summary>
    public static EllipseShape CreateCircle(double centerX, double centerY, double radius)
        => new(centerX, centerY, radius, radius);

    public override Rect2D GetLocalBounds()
        => Rect2D.FromCenter(CenterX, CenterY, RadiusX * 2, RadiusY * 2);

    public bool IsCircle => Math.Abs(RadiusX - RadiusY) < 0.0001;
}
