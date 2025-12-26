using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Represents a rectangle shape.
/// </summary>
public class RectangleShape : ShapeBase
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
    /// Creates a rectangle with the specified dimensions.
    /// </summary>
    public RectangleShape(double centerX, double centerY, double width, double height)
    {
        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Creates a square with the specified size.
    /// </summary>
    public static RectangleShape CreateSquare(double centerX, double centerY, double size)
        => new(centerX, centerY, size, size);

    public override Rect2D GetLocalBounds()
        => Rect2D.FromCenter(CenterX, CenterY, Width, Height);

    public bool IsSquare => Math.Abs(Width - Height) < 0.0001;
}
