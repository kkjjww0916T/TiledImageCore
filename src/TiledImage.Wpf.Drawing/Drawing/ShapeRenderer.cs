using System.Windows;
using System.Windows.Media;
using Geometry.Primitives;
using Geometry.Shapes.Shapes;
using Geometry.Shapes.Types;
using TiledImage.Wpf.Viewer;

namespace TiledImage.Wpf.Drawing;

/// <summary>
/// Renders IShape objects to WPF DrawingContext.
/// </summary>
public class ShapeRenderer
{
    private readonly ViewPortManager _viewPort;

    public ShapeRenderer(ViewPortManager viewPort)
    {
        _viewPort = viewPort;
    }

    /// <summary>
    /// Renders a shape to the DrawingContext.
    /// </summary>
    public void Render(DrawingContext dc, IShape shape, bool isSelected)
    {
        var style = isSelected ? ShapeStyle.Selected : shape.Style;
        var pen = style.ToPen(_viewPort.Zoom);
        var fill = style.ToFillBrush();

        // Apply rotation transform if needed
        if (shape is ShapeBase shapeBase && shapeBase.Rotation != 0)
        {
            var centerScreen = ImageToScreen(shape.CenterX, shape.CenterY);
            dc.PushTransform(new RotateTransform(shapeBase.Rotation, centerScreen.X, centerScreen.Y));
        }

        switch (shape)
        {
            case EllipseShape ellipse:
                RenderEllipse(dc, ellipse, fill, pen);
                break;
            case RectangleShape rect:
                RenderRectangle(dc, rect, fill, pen);
                break;
            case OblongShape oblong:
                RenderOblong(dc, oblong, fill, pen);
                break;
            case PolygonShape polygon:
                RenderPolygon(dc, polygon, fill, pen);
                break;
        }

        // Pop rotation transform
        if (shape is ShapeBase sb && sb.Rotation != 0)
        {
            dc.Pop();
        }
    }

    private void RenderEllipse(DrawingContext dc, EllipseShape ellipse, Brush fill, Pen pen)
    {
        var center = ImageToScreen(ellipse.CenterX, ellipse.CenterY);
        var radiusX = ellipse.RadiusX * _viewPort.Zoom;
        var radiusY = ellipse.RadiusY * _viewPort.Zoom;

        dc.DrawEllipse(fill, pen, center, radiusX, radiusY);
    }

    private void RenderRectangle(DrawingContext dc, RectangleShape rect, Brush fill, Pen pen)
    {
        var bounds = rect.GetLocalBounds();
        var screenRect = ImageRectToScreen(bounds);

        dc.DrawRectangle(fill, pen, screenRect);
    }

    private void RenderOblong(DrawingContext dc, OblongShape oblong, Brush fill, Pen pen)
    {
        // Create oblong geometry (rounded rectangle with semicircular ends)
        var bounds = oblong.GetLocalBounds();
        var screenRect = ImageRectToScreen(bounds);
        var radius = oblong.CapRadius * _viewPort.Zoom;

        // For oblong, the corner radius is the cap radius
        var geometry = new RectangleGeometry(screenRect, radius, radius);
        dc.DrawGeometry(fill, pen, geometry);
    }

    private void RenderPolygon(DrawingContext dc, PolygonShape polygon, Brush fill, Pen pen)
    {
        var points = polygon.GetAbsolutePoints().ToList();
        if (points.Count < 3) return;

        var screenPoints = points.Select(p => ImageToScreen(p.X, p.Y)).ToList();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(screenPoints[0], true, true);
            for (int i = 1; i < screenPoints.Count; i++)
            {
                ctx.LineTo(screenPoints[i], true, false);
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(fill, pen, geometry);
    }

    private Point ImageToScreen(double imageX, double imageY)
    {
        return new Point(
            _viewPort.ImageToScreenX(imageX),
            _viewPort.ImageToScreenY(imageY));
    }

    private Rect ImageRectToScreen(Rect2D imageRect)
    {
        var topLeft = ImageToScreen(imageRect.Left, imageRect.Top);
        var bottomRight = ImageToScreen(imageRect.Right, imageRect.Bottom);
        return new Rect(topLeft, bottomRight);
    }
}
