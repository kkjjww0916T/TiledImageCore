using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 2D rectangle with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Rect2<T>(T X, T Y, T Width, T Height)
    where T : struct, INumber<T>
{
    public static Rect2<T> Empty => new(T.Zero, T.Zero, T.Zero, T.Zero);

    #region Properties

    public T Left => X;
    public T Top => Y;
    public T Right => X + Width;
    public T Bottom => Y + Height;
    public Point2<T> Location => new(X, Y);
    public Size2<T> Size => new(Width, Height);
    public Point2<T> Center => new(X + Width / (T.One + T.One), Y + Height / (T.One + T.One));
    public T Area => Width * Height;
    public bool IsEmpty => Width <= T.Zero || Height <= T.Zero;

    public Point2<T> TopLeft => new(X, Y);
    public Point2<T> TopRight => new(Right, Y);
    public Point2<T> BottomLeft => new(X, Bottom);
    public Point2<T> BottomRight => new(Right, Bottom);

    #endregion

    #region Factory Methods

    public Rect2(Point2<T> location, Size2<T> size)
        : this(location.X, location.Y, size.Width, size.Height) { }

    public static Rect2<T> FromLTRB(T left, T top, T right, T bottom)
        => new(left, top, right - left, bottom - top);

    public static Rect2<T> FromCenter(Point2<T> center, Size2<T> size)
    {
        var two = T.One + T.One;
        return new(center.X - size.Width / two, center.Y - size.Height / two, size.Width, size.Height);
    }

    public static Rect2<T> FromCenter(T centerX, T centerY, T width, T height)
    {
        var two = T.One + T.One;
        return new(centerX - width / two, centerY - height / two, width, height);
    }

    public static Rect2<T> FromPoints(Point2<T> p1, Point2<T> p2)
    {
        var minX = T.Min(p1.X, p2.X);
        var minY = T.Min(p1.Y, p2.Y);
        var maxX = T.Max(p1.X, p2.X);
        var maxY = T.Max(p1.Y, p2.Y);
        return new(minX, minY, maxX - minX, maxY - minY);
    }

    public static Rect2<T> FromPoints(IEnumerable<Point2<T>> points)
    {
        using var enumerator = points.GetEnumerator();
        if (!enumerator.MoveNext()) return Empty;

        var first = enumerator.Current;
        var minX = first.X; var minY = first.Y;
        var maxX = first.X; var maxY = first.Y;

        while (enumerator.MoveNext())
        {
            var p = enumerator.Current;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new(minX, minY, maxX - minX, maxY - minY);
    }

    #endregion

    #region Methods

    public bool Contains(Point2<T> point)
        => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    public bool Contains(T x, T y)
        => x >= X && x < Right && y >= Y && y < Bottom;

    public bool Contains(Rect2<T> other)
        => X <= other.X && Right >= other.Right && Y <= other.Y && Bottom >= other.Bottom;

    public bool IntersectsWith(Rect2<T> other)
        => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public Rect2<T> Intersect(Rect2<T> other)
    {
        var left = T.Max(X, other.X);
        var top = T.Max(Y, other.Y);
        var right = T.Min(Right, other.Right);
        var bottom = T.Min(Bottom, other.Bottom);

        if (right <= left || bottom <= top)
            return Empty;

        return new(left, top, right - left, bottom - top);
    }

    public Rect2<T> Union(Rect2<T> other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var left = T.Min(X, other.X);
        var top = T.Min(Y, other.Y);
        var right = T.Max(Right, other.Right);
        var bottom = T.Max(Bottom, other.Bottom);

        return new(left, top, right - left, bottom - top);
    }

    public Rect2<T> Offset(T dx, T dy) => new(X + dx, Y + dy, Width, Height);
    public Rect2<T> Offset(Point2<T> offset) => new(X + offset.X, Y + offset.Y, Width, Height);

    public Rect2<T> Inflate(T dx, T dy)
    {
        var two = T.One + T.One;
        return new(X - dx, Y - dy, Width + two * dx, Height + two * dy);
    }

    public Rect2<T> Scale(T factor)
    {
        var center = Center;
        var newWidth = Width * factor;
        var newHeight = Height * factor;
        var two = T.One + T.One;
        return new(center.X - newWidth / two, center.Y - newHeight / two, newWidth, newHeight);
    }

    public Rect2<T> WithLocation(Point2<T> location) => new(location.X, location.Y, Width, Height);
    public Rect2<T> WithSize(Size2<T> size) => new(X, Y, size.Width, size.Height);

    public void Deconstruct(out T x, out T y, out T width, out T height)
    {
        x = X; y = Y; width = Width; height = Height;
    }

    #endregion

    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}
