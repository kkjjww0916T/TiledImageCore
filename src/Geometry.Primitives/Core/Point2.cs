using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 2D point/vector with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Point2<T>(T X, T Y)
    where T : struct, INumber<T>
{
    public static Point2<T> Zero => new(T.Zero, T.Zero);
    public static Point2<T> UnitX => new(T.One, T.Zero);
    public static Point2<T> UnitY => new(T.Zero, T.One);
    public static Point2<T> One => new(T.One, T.One);

    #region Operators

    public static Point2<T> operator +(Point2<T> a, Point2<T> b)
        => new(a.X + b.X, a.Y + b.Y);

    public static Point2<T> operator -(Point2<T> a, Point2<T> b)
        => new(a.X - b.X, a.Y - b.Y);

    public static Point2<T> operator -(Point2<T> a)
        => new(-a.X, -a.Y);

    public static Point2<T> operator *(Point2<T> a, T scalar)
        => new(a.X * scalar, a.Y * scalar);

    public static Point2<T> operator *(T scalar, Point2<T> a)
        => new(a.X * scalar, a.Y * scalar);

    public static Point2<T> operator /(Point2<T> a, T scalar)
        => new(a.X / scalar, a.Y / scalar);

    #endregion

    #region Methods

    public T LengthSquared() => X * X + Y * Y;

    public T DistanceSquaredTo(Point2<T> other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    public Point2<T> Offset(T dx, T dy) => new(X + dx, Y + dy);
    public Point2<T> WithX(T x) => new(x, Y);
    public Point2<T> WithY(T y) => new(X, y);
    public T Dot(Point2<T> other) => X * other.X + Y * other.Y;
    public T Cross(Point2<T> other) => X * other.Y - Y * other.X;
    public Point2<T> Perpendicular() => new(-Y, X);

    public void Deconstruct(out T x, out T y)
    {
        x = X;
        y = Y;
    }

    #endregion

    public override string ToString() => $"({X}, {Y})";
}
