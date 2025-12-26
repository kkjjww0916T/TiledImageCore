using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 3D box (axis-aligned bounding box) value object with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Box3<T>(T X, T Y, T Z, T Width, T Height, T Depth)
    where T : struct, INumber<T>
{
    /// <summary>
    /// Empty box at origin with zero size.
    /// </summary>
    public static Box3<T> Empty => new(T.Zero, T.Zero, T.Zero, T.Zero, T.Zero, T.Zero);

    #region Constructors

    /// <summary>
    /// Creates a box from location and size.
    /// </summary>
    public Box3(Point3<T> location, Size3<T> size)
        : this(location.X, location.Y, location.Z, size.Width, size.Height, size.Depth)
    {
    }

    /// <summary>
    /// Creates a 3D box from a 2D rect, Z position and depth.
    /// </summary>
    public Box3(Rect2<T> xy, T z, T depth)
        : this(xy.X, xy.Y, z, xy.Width, xy.Height, depth)
    {
    }

    #endregion

    #region Properties

    /// <summary>Front face (X).</summary>
    public T Left => X;

    /// <summary>Top face (Y).</summary>
    public T Top => Y;

    /// <summary>Near face (Z).</summary>
    public T Front => Z;

    /// <summary>Right face (X + Width).</summary>
    public T Right => X + Width;

    /// <summary>Bottom face (Y + Height).</summary>
    public T Bottom => Y + Height;

    /// <summary>Far face (Z + Depth).</summary>
    public T Back => Z + Depth;

    /// <summary>Location as Point3.</summary>
    public Point3<T> Location => new(X, Y, Z);

    /// <summary>Size as Size3.</summary>
    public Size3<T> Size => new(Width, Height, Depth);

    /// <summary>Center point.</summary>
    public Point3<T> Center
    {
        get
        {
            var two = T.One + T.One;
            return new(X + Width / two, Y + Height / two, Z + Depth / two);
        }
    }

    /// <summary>Volume (Width * Height * Depth).</summary>
    public T Volume => Width * Height * Depth;

    /// <summary>Returns true if any dimension is less than or equal to zero.</summary>
    public bool IsEmpty => Width <= T.Zero || Height <= T.Zero || Depth <= T.Zero;

    #endregion

    #region Projections

    /// <summary>XY plane rect (Z dropped).</summary>
    public Rect2<T> XYRect => new(X, Y, Width, Height);

    /// <summary>XZ plane rect (Y dropped).</summary>
    public Rect2<T> XZRect => new(X, Z, Width, Depth);

    /// <summary>YZ plane rect (X dropped).</summary>
    public Rect2<T> YZRect => new(Y, Z, Height, Depth);

    /// <summary>Gets the XY slice at a specific Z coordinate.</summary>
    public Rect2<T> SliceXY(T z) => new(X, Y, Width, Height);

    /// <summary>Gets the XZ slice at a specific Y coordinate.</summary>
    public Rect2<T> SliceXZ(T y) => new(X, Z, Width, Depth);

    /// <summary>Gets the YZ slice at a specific X coordinate.</summary>
    public Rect2<T> SliceYZ(T x) => new(Y, Z, Height, Depth);

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a box from min and max corner points.
    /// </summary>
    public static Box3<T> FromMinMax(Point3<T> min, Point3<T> max)
        => new(min.X, min.Y, min.Z, max.X - min.X, max.Y - min.Y, max.Z - min.Z);

    /// <summary>
    /// Creates a box centered at a point with the given size.
    /// </summary>
    public static Box3<T> FromCenter(Point3<T> center, Size3<T> size)
    {
        var two = T.One + T.One;
        return new(
            center.X - size.Width / two,
            center.Y - size.Height / two,
            center.Z - size.Depth / two,
            size.Width, size.Height, size.Depth);
    }

    /// <summary>
    /// Creates a box from two corner points (any diagonal corners).
    /// </summary>
    public static Box3<T> FromPoints(Point3<T> p1, Point3<T> p2)
    {
        var minX = T.Min(p1.X, p2.X);
        var minY = T.Min(p1.Y, p2.Y);
        var minZ = T.Min(p1.Z, p2.Z);
        var maxX = T.Max(p1.X, p2.X);
        var maxY = T.Max(p1.Y, p2.Y);
        var maxZ = T.Max(p1.Z, p2.Z);
        return new(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns true if the point is inside this box.
    /// </summary>
    public bool Contains(Point3<T> point)
        => point.X >= X && point.X < Right &&
           point.Y >= Y && point.Y < Bottom &&
           point.Z >= Z && point.Z < Back;

    /// <summary>
    /// Returns true if this box completely contains another box.
    /// </summary>
    public bool Contains(Box3<T> other)
        => X <= other.X && Right >= other.Right &&
           Y <= other.Y && Bottom >= other.Bottom &&
           Z <= other.Z && Back >= other.Back;

    /// <summary>
    /// Returns true if this box intersects with another box.
    /// </summary>
    public bool IntersectsWith(Box3<T> other)
        => X < other.Right && Right > other.X &&
           Y < other.Bottom && Bottom > other.Y &&
           Z < other.Back && Back > other.Z;

    /// <summary>
    /// Returns the intersection of two boxes, or Empty if they don't intersect.
    /// </summary>
    public Box3<T> Intersect(Box3<T> other)
    {
        var left = T.Max(X, other.X);
        var top = T.Max(Y, other.Y);
        var front = T.Max(Z, other.Z);
        var right = T.Min(Right, other.Right);
        var bottom = T.Min(Bottom, other.Bottom);
        var back = T.Min(Back, other.Back);

        if (right <= left || bottom <= top || back <= front)
            return Empty;

        return new(left, top, front, right - left, bottom - top, back - front);
    }

    /// <summary>
    /// Returns the smallest box that contains both boxes.
    /// </summary>
    public Box3<T> Union(Box3<T> other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var left = T.Min(X, other.X);
        var top = T.Min(Y, other.Y);
        var front = T.Min(Z, other.Z);
        var right = T.Max(Right, other.Right);
        var bottom = T.Max(Bottom, other.Bottom);
        var back = T.Max(Back, other.Back);

        return new(left, top, front, right - left, bottom - top, back - front);
    }

    /// <summary>
    /// Returns a new box offset by (dx, dy, dz).
    /// </summary>
    public Box3<T> Offset(T dx, T dy, T dz) => new(X + dx, Y + dy, Z + dz, Width, Height, Depth);

    /// <summary>
    /// Returns a new box offset by a point.
    /// </summary>
    public Box3<T> Offset(Point3<T> offset) => new(X + offset.X, Y + offset.Y, Z + offset.Z, Width, Height, Depth);

    /// <summary>
    /// Returns a new box inflated by (dx, dy, dz) on each side.
    /// </summary>
    public Box3<T> Inflate(T dx, T dy, T dz)
    {
        var two = T.One + T.One;
        return new(X - dx, Y - dy, Z - dz, Width + two * dx, Height + two * dy, Depth + two * dz);
    }

    /// <summary>
    /// Returns a new box with a different location.
    /// </summary>
    public Box3<T> WithLocation(Point3<T> location) => new(location.X, location.Y, location.Z, Width, Height, Depth);

    /// <summary>
    /// Returns a new box with a different size.
    /// </summary>
    public Box3<T> WithSize(Size3<T> size) => new(X, Y, Z, size.Width, size.Height, size.Depth);

    /// <summary>
    /// Deconstructs the box into its components.
    /// </summary>
    public void Deconstruct(out T x, out T y, out T z, out T width, out T height, out T depth)
    {
        x = X;
        y = Y;
        z = Z;
        width = Width;
        height = Height;
        depth = Depth;
    }

    #endregion

    public override string ToString() => $"({X}, {Y}, {Z}, {Width}, {Height}, {Depth})";
}
