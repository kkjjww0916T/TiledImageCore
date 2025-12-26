using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Represents an oblong (capsule/stadium) shape.
/// A rectangle with semicircular ends on the shorter sides.
/// </summary>
public class OblongShape : ShapeBase
{
    private double _width;
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Max(0, value));
    }

    private double _height;
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, Math.Max(0, value));
    }

    /// <summary>
    /// The radius of the semicircular ends (half of the shorter dimension).
    /// </summary>
    public double CapRadius => Math.Min(Width, Height) / 2;

    /// <summary>
    /// True if the oblong is horizontal (width > height).
    /// </summary>
    public bool IsHorizontal => Width >= Height;

    /// <summary>
    /// Creates an oblong with the specified dimensions.
    /// </summary>
    public OblongShape(double centerX, double centerY, double width, double height)
    {
        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
    }

    public override Rect2D GetLocalBounds()
        => Rect2D.FromCenter(CenterX, CenterY, Width, Height);

    /// <summary>
    /// Gets the center points of the two semicircular caps.
    /// </summary>
    public (Point2D Cap1, Point2D Cap2) GetCapCenters()
    {
        var radius = CapRadius;

        if (IsHorizontal)
        {
            // Horizontal: caps on left and right
            var halfRectWidth = Width / 2 - radius;
            return (
                new Point2D(CenterX - halfRectWidth, CenterY),
                new Point2D(CenterX + halfRectWidth, CenterY)
            );
        }
        else
        {
            // Vertical: caps on top and bottom
            var halfRectHeight = Height / 2 - radius;
            return (
                new Point2D(CenterX, CenterY - halfRectHeight),
                new Point2D(CenterX, CenterY + halfRectHeight)
            );
        }
    }

    /// <summary>
    /// Gets the rectangular part bounds (between the two caps).
    /// </summary>
    public Rect2D GetRectanglePart()
    {
        var radius = CapRadius;

        if (IsHorizontal)
        {
            return Rect2D.FromCenter(CenterX, CenterY, Width - 2 * radius, Height);
        }
        else
        {
            return Rect2D.FromCenter(CenterX, CenterY, Width, Height - 2 * radius);
        }
    }
}
