using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 3D point/vector value object with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Point3<T>(T X, T Y, T Z)
    where T : struct, INumber<T>
{
    /// <summary>
    /// Zero point (0, 0, 0).
    /// </summary>
    public static Point3<T> Zero => new(T.Zero, T.Zero, T.Zero);

    /// <summary>
    /// Unit X vector (1, 0, 0).
    /// </summary>
    public static Point3<T> UnitX => new(T.One, T.Zero, T.Zero);

    /// <summary>
    /// Unit Y vector (0, 1, 0).
    /// </summary>
    public static Point3<T> UnitY => new(T.Zero, T.One, T.Zero);

    /// <summary>
    /// Unit Z vector (0, 0, 1).
    /// </summary>
    public static Point3<T> UnitZ => new(T.Zero, T.Zero, T.One);

    /// <summary>
    /// One point (1, 1, 1).
    /// </summary>
    public static Point3<T> One => new(T.One, T.One, T.One);

    #region Constructors

    /// <summary>
    /// Creates a 3D point from a 2D point and Z value.
    /// </summary>
    public Point3(Point2<T> xy, T z) : this(xy.X, xy.Y, z) { }

    #endregion

    #region Operators

    public static Point3<T> operator +(Point3<T> a, Point3<T> b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Point3<T> operator -(Point3<T> a, Point3<T> b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Point3<T> operator -(Point3<T> a)
        => new(-a.X, -a.Y, -a.Z);

    public static Point3<T> operator *(Point3<T> a, T scalar)
        => new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Point3<T> operator *(T scalar, Point3<T> a)
        => new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Point3<T> operator /(Point3<T> a, T scalar)
        => new(a.X / scalar, a.Y / scalar, a.Z / scalar);

    #endregion

    #region Projections

    /// <summary>XY projection (Z dropped).</summary>
    public Point2<T> XY => new(X, Y);

    /// <summary>XZ projection (Y dropped).</summary>
    public Point2<T> XZ => new(X, Z);

    /// <summary>YZ projection (X dropped).</summary>
    public Point2<T> YZ => new(Y, Z);

    #endregion

    #region Methods

    /// <summary>
    /// Returns the squared length of this vector.
    /// </summary>
    public T LengthSquared() => X * X + Y * Y + Z * Z;

    /// <summary>
    /// Returns the squared distance to another point.
    /// </summary>
    public T DistanceSquaredTo(Point3<T> other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        var dz = other.Z - Z;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Returns a new point offset by (dx, dy, dz).
    /// </summary>
    public Point3<T> Offset(T dx, T dy, T dz) => new(X + dx, Y + dy, Z + dz);

    /// <summary>
    /// Returns a new point with a different X value.
    /// </summary>
    public Point3<T> WithX(T x) => new(x, Y, Z);

    /// <summary>
    /// Returns a new point with a different Y value.
    /// </summary>
    public Point3<T> WithY(T y) => new(X, y, Z);

    /// <summary>
    /// Returns a new point with a different Z value.
    /// </summary>
    public Point3<T> WithZ(T z) => new(X, Y, z);

    /// <summary>
    /// Returns the dot product with another vector.
    /// </summary>
    public T Dot(Point3<T> other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>
    /// Returns the cross product with another vector.
    /// </summary>
    public Point3<T> Cross(Point3<T> other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    /// <summary>
    /// Creates a 3D point from XY plane with Z value.
    /// </summary>
    public static Point3<T> FromXY(Point2<T> xy, T z) => new(xy.X, xy.Y, z);

    /// <summary>
    /// Creates a 3D point from XZ plane with Y value.
    /// </summary>
    public static Point3<T> FromXZ(Point2<T> xz, T y) => new(xz.X, y, xz.Y);

    /// <summary>
    /// Creates a 3D point from YZ plane with X value.
    /// </summary>
    public static Point3<T> FromYZ(Point2<T> yz, T x) => new(x, yz.X, yz.Y);

    /// <summary>
    /// Deconstructs the point into its components.
    /// </summary>
    public void Deconstruct(out T x, out T y, out T z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    #endregion

    public override string ToString() => $"({X}, {Y}, {Z})";
}
