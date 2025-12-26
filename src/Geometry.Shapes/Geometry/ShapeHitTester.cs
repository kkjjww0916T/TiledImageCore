using Geometry.Primitives;
using Geometry.Shapes.Shapes;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Geometry;

/// <summary>
/// Provides hit testing functionality for shapes.
/// </summary>
public static class ShapeHitTester
{
    /// <summary>
    /// Tests if a point is inside the shape.
    /// </summary>
    public static bool HitTest(IShape shape, double imageX, double imageY)
    {
        // First, quick bounds check with world bounds
        var worldBounds = shape.GetWorldBounds();
        if (!worldBounds.Contains(imageX, imageY))
            return false;

        // Transform point to local coordinates (undo rotation)
        var localPoint = TransformToLocal(shape, imageX, imageY);

        return shape switch
        {
            EllipseShape ellipse => HitTestEllipse(ellipse, localPoint.X, localPoint.Y),
            RectangleShape rect => HitTestRectangle(rect, localPoint.X, localPoint.Y),
            OblongShape oblong => HitTestOblong(oblong, localPoint.X, localPoint.Y),
            PolygonShape polygon => HitTestPolygon(polygon, imageX, imageY), // Polygon uses world coords
            _ => false
        };
    }

    /// <summary>
    /// Finds the topmost shape at the specified point.
    /// Searches in reverse order (last = topmost).
    /// </summary>
    public static IShape? HitTestShapes(IEnumerable<IShape> shapes, double imageX, double imageY)
    {
        // Reverse to check topmost first
        foreach (var shape in shapes.Reverse())
        {
            if (HitTest(shape, imageX, imageY))
                return shape;
        }
        return null;
    }

    /// <summary>
    /// Finds all shapes that contain the specified point.
    /// </summary>
    public static IEnumerable<IShape> HitTestAll(IEnumerable<IShape> shapes, double imageX, double imageY)
    {
        foreach (var shape in shapes)
        {
            if (HitTest(shape, imageX, imageY))
                yield return shape;
        }
    }

    /// <summary>
    /// Transforms a point from image coordinates to shape-local coordinates (rotation removed).
    /// </summary>
    private static Point2D TransformToLocal(IShape shape, double imageX, double imageY)
    {
        if (shape is ShapeBase shapeBase && shapeBase.Rotation != 0)
        {
            var point = new Point2D(imageX, imageY);
            var center = new Point2D(shape.CenterX, shape.CenterY);
            // Rotate backwards (negative angle)
            return point.RotateAroundDegrees(center, -shapeBase.Rotation);
        }

        return new Point2D(imageX, imageY);
    }

    private static bool HitTestEllipse(EllipseShape ellipse, double x, double y)
    {
        // Ellipse equation: (x-cx)²/rx² + (y-cy)²/ry² <= 1
        var dx = x - ellipse.CenterX;
        var dy = y - ellipse.CenterY;
        var rx = ellipse.RadiusX;
        var ry = ellipse.RadiusY;

        if (rx <= 0 || ry <= 0) return false;

        return (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0;
    }

    private static bool HitTestRectangle(RectangleShape rect, double x, double y)
    {
        var halfW = rect.Width / 2;
        var halfH = rect.Height / 2;

        return x >= rect.CenterX - halfW && x <= rect.CenterX + halfW &&
               y >= rect.CenterY - halfH && y <= rect.CenterY + halfH;
    }

    private static bool HitTestOblong(OblongShape oblong, double x, double y)
    {
        var radius = oblong.CapRadius;
        if (radius <= 0) return false;

        var dx = x - oblong.CenterX;
        var dy = y - oblong.CenterY;

        if (oblong.IsHorizontal)
        {
            // Horizontal capsule
            var halfRectWidth = oblong.Width / 2 - radius;

            // Check center rectangle
            if (Math.Abs(dx) <= halfRectWidth && Math.Abs(dy) <= radius)
                return true;

            // Check left cap
            if (dx < -halfRectWidth)
            {
                var capDx = dx + halfRectWidth;
                return capDx * capDx + dy * dy <= radius * radius;
            }

            // Check right cap
            if (dx > halfRectWidth)
            {
                var capDx = dx - halfRectWidth;
                return capDx * capDx + dy * dy <= radius * radius;
            }

            return false;
        }
        else
        {
            // Vertical capsule
            var halfRectHeight = oblong.Height / 2 - radius;

            // Check center rectangle
            if (Math.Abs(dx) <= radius && Math.Abs(dy) <= halfRectHeight)
                return true;

            // Check top cap
            if (dy < -halfRectHeight)
            {
                var capDy = dy + halfRectHeight;
                return dx * dx + capDy * capDy <= radius * radius;
            }

            // Check bottom cap
            if (dy > halfRectHeight)
            {
                var capDy = dy - halfRectHeight;
                return dx * dx + capDy * capDy <= radius * radius;
            }

            return false;
        }
    }

    private static bool HitTestPolygon(PolygonShape polygon, double x, double y)
    {
        var points = polygon.GetAbsolutePoints().ToList();
        if (points.Count < 3) return false;

        // Ray casting algorithm
        var inside = false;
        var n = points.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = points[i];
            var pj = points[j];

            if ((pi.Y > y) != (pj.Y > y) &&
                x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
