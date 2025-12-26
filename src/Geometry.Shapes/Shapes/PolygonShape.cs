using System.Collections.ObjectModel;
using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Represents a polygon shape defined by a series of points.
/// Points are stored relative to the center.
/// </summary>
public class PolygonShape : ShapeBase
{
    private readonly ObservableCollection<Point2D> _relativePoints = new();

    /// <summary>
    /// Points relative to the center of the polygon.
    /// </summary>
    public IReadOnlyList<Point2D> RelativePoints => _relativePoints;

    /// <summary>
    /// Gets the absolute points in image coordinates (without rotation).
    /// Rotation is handled by the renderer using the Rotation property.
    /// </summary>
    public IEnumerable<Point2D> GetAbsolutePoints()
    {
        foreach (var point in _relativePoints)
        {
            yield return new Point2D(CenterX + point.X, CenterY + point.Y);
        }
    }

    /// <summary>
    /// Creates a polygon with no points.
    /// </summary>
    public PolygonShape(double centerX, double centerY)
    {
        CenterX = centerX;
        CenterY = centerY;
    }

    /// <summary>
    /// Creates a polygon from absolute points (will calculate center and relative points).
    /// </summary>
    public static PolygonShape FromAbsolutePoints(IEnumerable<Point2D> absolutePoints)
    {
        var points = absolutePoints.ToList();
        if (points.Count == 0)
        {
            return new PolygonShape(0, 0);
        }

        // Calculate centroid
        var centerX = points.Average(p => p.X);
        var centerY = points.Average(p => p.Y);

        var polygon = new PolygonShape(centerX, centerY);

        foreach (var point in points)
        {
            polygon.AddPoint(point.X - centerX, point.Y - centerY);
        }

        return polygon;
    }

    /// <summary>
    /// Creates a regular polygon with the specified number of sides.
    /// </summary>
    public static PolygonShape CreateRegular(double centerX, double centerY, int sides, double radius)
    {
        if (sides < 3) throw new ArgumentException("Polygon must have at least 3 sides", nameof(sides));

        var polygon = new PolygonShape(centerX, centerY);
        var angleStep = 2 * Math.PI / sides;

        for (int i = 0; i < sides; i++)
        {
            // Start from top (negative Y in image coordinates)
            var angle = -Math.PI / 2 + i * angleStep;
            var x = radius * Math.Cos(angle);
            var y = radius * Math.Sin(angle);
            polygon.AddPoint(x, y);
        }

        return polygon;
    }

    /// <summary>
    /// Adds a point relative to the center.
    /// </summary>
    public void AddPoint(double relativeX, double relativeY)
    {
        _relativePoints.Add(new Point2D(relativeX, relativeY));
        OnPropertyChanged(nameof(RelativePoints));
    }

    /// <summary>
    /// Inserts a point at the specified index.
    /// </summary>
    public void InsertPoint(int index, double relativeX, double relativeY)
    {
        _relativePoints.Insert(index, new Point2D(relativeX, relativeY));
        OnPropertyChanged(nameof(RelativePoints));
    }

    /// <summary>
    /// Removes the point at the specified index.
    /// </summary>
    public void RemovePointAt(int index)
    {
        _relativePoints.RemoveAt(index);
        OnPropertyChanged(nameof(RelativePoints));
    }

    /// <summary>
    /// Updates a point at the specified index.
    /// </summary>
    public void SetPoint(int index, double relativeX, double relativeY)
    {
        _relativePoints[index] = new Point2D(relativeX, relativeY);
        OnPropertyChanged(nameof(RelativePoints));
    }

    /// <summary>
    /// Clears all points.
    /// </summary>
    public void ClearPoints()
    {
        _relativePoints.Clear();
        OnPropertyChanged(nameof(RelativePoints));
    }

    public override Rect2D GetLocalBounds()
    {
        if (_relativePoints.Count == 0)
            return Rect2D.FromCenter(CenterX, CenterY, 0, 0);

        var absolutePoints = _relativePoints.Select(p => new Point2D(CenterX + p.X, CenterY + p.Y));
        return Rect2D.FromPoints(absolutePoints);
    }

    // GetWorldBounds()는 ShapeBase에서 GetLocalBounds() + Rotation 적용하여 처리
}
