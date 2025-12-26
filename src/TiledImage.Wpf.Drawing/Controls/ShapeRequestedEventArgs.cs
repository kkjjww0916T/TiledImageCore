using System.Windows;
using Geometry.Primitives;
using Geometry.Shapes.Shapes;
using Geometry.Shapes.Types;

namespace TiledImage.Wpf.Drawing;

/// <summary>
/// Event args for shape creation request.
/// Contains all information needed to create a shape.
/// </summary>
public class ShapeCreationRequestedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// The creation mode (shape type).
    /// </summary>
    public ShapeCreationMode CreationMode { get; }

    /// <summary>
    /// Center X coordinate in image space.
    /// </summary>
    public double CenterX { get; }

    /// <summary>
    /// Center Y coordinate in image space.
    /// </summary>
    public double CenterY { get; }

    /// <summary>
    /// Width of the shape (or RadiusX * 2 for ellipse/circle).
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Height of the shape (or RadiusY * 2 for ellipse/circle).
    /// </summary>
    public double Height { get; }

    /// <summary>
    /// Style to apply to the new shape.
    /// </summary>
    public ShapeStyle Style { get; }

    /// <summary>
    /// Relative points for polygon shapes (null for other shapes).
    /// </summary>
    public IReadOnlyList<Point2D>? RelativePoints { get; }

    public ShapeCreationRequestedEventArgs(
        RoutedEvent routedEvent,
        ShapeCreationMode creationMode,
        double centerX,
        double centerY,
        double width,
        double height,
        ShapeStyle style,
        IReadOnlyList<Point2D>? relativePoints = null)
        : base(routedEvent)
    {
        CreationMode = creationMode;
        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
        Style = style;
        RelativePoints = relativePoints;
    }
}

/// <summary>
/// Event args for shape delete request.
/// ViewModel should delete shapes from SelectedShapes.
/// </summary>
public class ShapeDeleteRequestedEventArgs : RoutedEventArgs
{
    public ShapeDeleteRequestedEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }
}

/// <summary>
/// Event args for shape copy request.
/// ViewModel should copy SelectedShapes to clipboard.
/// </summary>
public class ShapeCopyRequestedEventArgs : RoutedEventArgs
{
    public ShapeCopyRequestedEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }
}

/// <summary>
/// Event args for shape paste request.
/// ViewModel should paste shapes from clipboard.
/// </summary>
public class ShapePasteRequestedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Paste position in image coordinates (current mouse position).
    /// </summary>
    public double ImageX { get; }

    /// <summary>
    /// Paste position in image coordinates (current mouse position).
    /// </summary>
    public double ImageY { get; }

    public ShapePasteRequestedEventArgs(RoutedEvent routedEvent, double imageX, double imageY)
        : base(routedEvent)
    {
        ImageX = imageX;
        ImageY = imageY;
    }
}

/// <summary>
/// Event args for shape resize request.
/// ViewModel should update the shape's dimensions.
/// </summary>
public class ShapeResizeRequestedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// The shape being resized.
    /// </summary>
    public IShape Shape { get; }

    /// <summary>
    /// New center X coordinate in image space.
    /// </summary>
    public double NewCenterX { get; }

    /// <summary>
    /// New center Y coordinate in image space.
    /// </summary>
    public double NewCenterY { get; }

    /// <summary>
    /// New width of the shape.
    /// </summary>
    public double NewWidth { get; }

    /// <summary>
    /// New height of the shape.
    /// </summary>
    public double NewHeight { get; }

    public ShapeResizeRequestedEventArgs(
        RoutedEvent routedEvent,
        IShape shape,
        double newCenterX,
        double newCenterY,
        double newWidth,
        double newHeight)
        : base(routedEvent)
    {
        Shape = shape;
        NewCenterX = newCenterX;
        NewCenterY = newCenterY;
        NewWidth = newWidth;
        NewHeight = newHeight;
    }
}
