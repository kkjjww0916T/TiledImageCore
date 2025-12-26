using Geometry.Primitives;
using TiledImage.Transforms;
using TiledImage.Transforms.Operations;

namespace TiledImage.Wpf.Mapping;

/// <summary>
/// Interactive perspective editor with corner dragging support.
/// Wraps PerspectiveTransform for stateful UI editing.
/// </summary>
public class PerspectiveEditor
{
    private Point2F[] _sourceCorners;
    private Point2F[] _destinationCorners;
    private Matrix3x3 _transformMatrix;
    private Matrix3x3 _inverseMatrix;
    private bool _isDirty = true;

    /// <summary>
    /// Raised when the transform corners change.
    /// </summary>
    public event EventHandler? TransformChanged;

    /// <summary>
    /// Source corners in image coordinates (original quad).
    /// Order: TopLeft, TopRight, BottomRight, BottomLeft
    /// </summary>
    public Point2F[] SourceCorners
    {
        get => _sourceCorners;
        set
        {
            if (value.Length != 4)
                throw new ArgumentException("Must provide exactly 4 corners");
            _sourceCorners = value;
            _isDirty = true;
            TransformChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Destination corners in image coordinates (transformed quad).
    /// Order: TopLeft, TopRight, BottomRight, BottomLeft
    /// </summary>
    public Point2F[] DestinationCorners
    {
        get => _destinationCorners;
        set
        {
            if (value.Length != 4)
                throw new ArgumentException("Must provide exactly 4 corners");
            _destinationCorners = value;
            _isDirty = true;
            TransformChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets whether the transform has been modified from identity.
    /// </summary>
    public bool HasTransform { get; private set; }

    public PerspectiveEditor()
    {
        _sourceCorners = new Point2F[4];
        _destinationCorners = new Point2F[4];
        _transformMatrix = Matrix3x3.Identity;
        _inverseMatrix = Matrix3x3.Identity;
    }

    /// <summary>
    /// Initialize corners to match the image bounds (no transformation).
    /// </summary>
    public void InitializeForImage(long imageWidth, long imageHeight)
    {
        var corners = new Point2F[]
        {
            new(0, 0),
            new(imageWidth, 0),
            new(imageWidth, imageHeight),
            new(0, imageHeight)
        };

        _sourceCorners = corners;
        _destinationCorners = new Point2F[]
        {
            new(corners[0].X, corners[0].Y),
            new(corners[1].X, corners[1].Y),
            new(corners[2].X, corners[2].Y),
            new(corners[3].X, corners[3].Y)
        };
        _isDirty = true;
        HasTransform = false;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Move a single corner point.
    /// </summary>
    public void MoveCorner(int cornerIndex, Point2F newPosition)
    {
        if (cornerIndex < 0 || cornerIndex >= 4)
            throw new ArgumentOutOfRangeException(nameof(cornerIndex));

        _destinationCorners[cornerIndex] = newPosition;
        _isDirty = true;
        HasTransform = true;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Move a corner by delta.
    /// </summary>
    public void MoveCornerBy(int cornerIndex, float deltaX, float deltaY)
    {
        if (cornerIndex < 0 || cornerIndex >= 4)
            throw new ArgumentOutOfRangeException(nameof(cornerIndex));

        var current = _destinationCorners[cornerIndex];
        _destinationCorners[cornerIndex] = current.Offset(deltaX, deltaY);
        _isDirty = true;
        HasTransform = true;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Set all four destination corners at once.
    /// </summary>
    public void SetDestinationCorners(Point2F topLeft, Point2F topRight, Point2F bottomRight, Point2F bottomLeft)
    {
        _destinationCorners[0] = topLeft;
        _destinationCorners[1] = topRight;
        _destinationCorners[2] = bottomRight;
        _destinationCorners[3] = bottomLeft;
        _isDirty = true;
        HasTransform = !AreCornersSame(_sourceCorners, _destinationCorners);
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool AreCornersSame(Point2F[] src, Point2F[] dst)
    {
        const float epsilon = 0.001f;
        for (int i = 0; i < 4; i++)
        {
            if (MathF.Abs(src[i].X - dst[i].X) > epsilon ||
                MathF.Abs(src[i].Y - dst[i].Y) > epsilon)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Reset transformation to identity.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < 4; i++)
        {
            _destinationCorners[i] = new Point2F(_sourceCorners[i].X, _sourceCorners[i].Y);
        }
        _isDirty = true;
        HasTransform = false;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Calculate the scale factor of the perspective transform.
    /// </summary>
    public float GetScaleFactor()
    {
        return PerspectiveTransform.CalculateScaleFactor(_sourceCorners, _destinationCorners);
    }

    /// <summary>
    /// Get the transformation matrix (source -> destination).
    /// </summary>
    public Matrix3x3 GetTransformMatrix()
    {
        if (_isDirty)
        {
            RecalculateMatrix();
        }
        return _transformMatrix;
    }

    /// <summary>
    /// Get the inverse transformation matrix (destination -> source).
    /// </summary>
    public Matrix3x3 GetInverseMatrix()
    {
        if (_isDirty)
        {
            RecalculateMatrix();
        }
        return _inverseMatrix;
    }

    /// <summary>
    /// Transform a point from source to destination coordinates.
    /// </summary>
    public Point2F TransformPoint(Point2F point)
    {
        var matrix = GetTransformMatrix();
        return matrix.MapPoint(point);
    }

    /// <summary>
    /// Transform a point from destination to source coordinates.
    /// </summary>
    public Point2F InverseTransformPoint(Point2F point)
    {
        var matrix = GetInverseMatrix();
        return matrix.MapPoint(point);
    }

    private void RecalculateMatrix()
    {
        _transformMatrix = PerspectiveTransform.ComputeHomography(_sourceCorners, _destinationCorners);

        if (_transformMatrix.TryInvert(out var inverse))
        {
            _inverseMatrix = inverse;
        }
        else
        {
            _inverseMatrix = Matrix3x3.Identity;
        }

        _isDirty = false;
    }

    /// <summary>
    /// Get the corner index closest to the given point within the specified threshold.
    /// </summary>
    public int GetCornerAtPoint(Point2F point, float threshold)
    {
        return PerspectiveTransform.GetCornerAtPoint(_destinationCorners, point, threshold);
    }

    /// <summary>
    /// Check if a point is inside the destination quadrilateral.
    /// </summary>
    public bool ContainsPoint(Point2F point)
    {
        return PerspectiveTransform.IsPointInQuad(_destinationCorners, point);
    }
}
