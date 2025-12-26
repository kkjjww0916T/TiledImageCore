using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 2D size value object with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Size2<T>(T Width, T Height)
    where T : struct, INumber<T>
{
    /// <summary>
    /// Empty size (0, 0).
    /// </summary>
    public static Size2<T> Empty => new(T.Zero, T.Zero);

    /// <summary>
    /// Unit size (1, 1).
    /// </summary>
    public static Size2<T> One => new(T.One, T.One);

    /// <summary>
    /// Gets the area (Width * Height).
    /// </summary>
    public T Area => Width * Height;

    /// <summary>
    /// Returns true if width or height is less than or equal to zero.
    /// </summary>
    public bool IsEmpty => Width <= T.Zero || Height <= T.Zero;

    #region Operators

    public static Size2<T> operator *(Size2<T> size, T scalar)
        => new(size.Width * scalar, size.Height * scalar);

    public static Size2<T> operator *(T scalar, Size2<T> size)
        => new(size.Width * scalar, size.Height * scalar);

    public static Size2<T> operator /(Size2<T> size, T scalar)
        => new(size.Width / scalar, size.Height / scalar);

    public static Size2<T> operator +(Size2<T> a, Size2<T> b)
        => new(a.Width + b.Width, a.Height + b.Height);

    public static Size2<T> operator -(Size2<T> a, Size2<T> b)
        => new(a.Width - b.Width, a.Height - b.Height);

    #endregion

    #region Methods

    /// <summary>
    /// Returns a new size scaled by the given factor.
    /// </summary>
    public Size2<T> Scale(T factor) => new(Width * factor, Height * factor);

    /// <summary>
    /// Returns a new size with a different width.
    /// </summary>
    public Size2<T> WithWidth(T width) => new(width, Height);

    /// <summary>
    /// Returns a new size with a different height.
    /// </summary>
    public Size2<T> WithHeight(T height) => new(Width, height);

    /// <summary>
    /// Converts to a Point2 (Width -> X, Height -> Y).
    /// </summary>
    public Point2<T> ToPoint() => new(Width, Height);

    /// <summary>
    /// Creates a Size2 from a Point2 (X -> Width, Y -> Height).
    /// </summary>
    public static Size2<T> FromPoint(Point2<T> point) => new(point.X, point.Y);

    /// <summary>
    /// Deconstructs the size into its components.
    /// </summary>
    public void Deconstruct(out T width, out T height)
    {
        width = Width;
        height = Height;
    }

    #endregion

    public override string ToString() => $"{Width} x {Height}";
}
