using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// 3D size value object with generic numeric type.
/// </summary>
/// <typeparam name="T">Numeric type (float, double, int, long, etc.)</typeparam>
public readonly record struct Size3<T>(T Width, T Height, T Depth)
    where T : struct, INumber<T>
{
    /// <summary>
    /// Empty size (0, 0, 0).
    /// </summary>
    public static Size3<T> Empty => new(T.Zero, T.Zero, T.Zero);

    /// <summary>
    /// Unit size (1, 1, 1).
    /// </summary>
    public static Size3<T> One => new(T.One, T.One, T.One);

    #region Constructors

    /// <summary>
    /// Creates a 3D size from a 2D size and depth.
    /// </summary>
    public Size3(Size2<T> wh, T depth) : this(wh.Width, wh.Height, depth) { }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the volume (Width * Height * Depth).
    /// </summary>
    public T Volume => Width * Height * Depth;

    /// <summary>
    /// Returns true if any dimension is less than or equal to zero.
    /// </summary>
    public bool IsEmpty => Width <= T.Zero || Height <= T.Zero || Depth <= T.Zero;

    /// <summary>
    /// WH projection (Width, Height only).
    /// </summary>
    public Size2<T> WH => new(Width, Height);

    /// <summary>
    /// WD projection (Width, Depth only).
    /// </summary>
    public Size2<T> WD => new(Width, Depth);

    /// <summary>
    /// HD projection (Height, Depth only).
    /// </summary>
    public Size2<T> HD => new(Height, Depth);

    #endregion

    #region Operators

    public static Size3<T> operator *(Size3<T> size, T scalar)
        => new(size.Width * scalar, size.Height * scalar, size.Depth * scalar);

    public static Size3<T> operator *(T scalar, Size3<T> size)
        => new(size.Width * scalar, size.Height * scalar, size.Depth * scalar);

    public static Size3<T> operator /(Size3<T> size, T scalar)
        => new(size.Width / scalar, size.Height / scalar, size.Depth / scalar);

    public static Size3<T> operator +(Size3<T> a, Size3<T> b)
        => new(a.Width + b.Width, a.Height + b.Height, a.Depth + b.Depth);

    public static Size3<T> operator -(Size3<T> a, Size3<T> b)
        => new(a.Width - b.Width, a.Height - b.Height, a.Depth - b.Depth);

    #endregion

    #region Methods

    /// <summary>
    /// Returns a new size scaled by the given factor.
    /// </summary>
    public Size3<T> Scale(T factor) => new(Width * factor, Height * factor, Depth * factor);

    /// <summary>
    /// Returns a new size with a different width.
    /// </summary>
    public Size3<T> WithWidth(T width) => new(width, Height, Depth);

    /// <summary>
    /// Returns a new size with a different height.
    /// </summary>
    public Size3<T> WithHeight(T height) => new(Width, height, Depth);

    /// <summary>
    /// Returns a new size with a different depth.
    /// </summary>
    public Size3<T> WithDepth(T depth) => new(Width, Height, depth);

    /// <summary>
    /// Converts to a Point3 (Width -> X, Height -> Y, Depth -> Z).
    /// </summary>
    public Point3<T> ToPoint() => new(Width, Height, Depth);

    /// <summary>
    /// Creates a Size3 from a Point3 (X -> Width, Y -> Height, Z -> Depth).
    /// </summary>
    public static Size3<T> FromPoint(Point3<T> point) => new(point.X, point.Y, point.Z);

    /// <summary>
    /// Creates a Size3 from a 2D size and depth.
    /// </summary>
    public static Size3<T> FromWH(Size2<T> wh, T depth) => new(wh.Width, wh.Height, depth);

    /// <summary>
    /// Deconstructs the size into its components.
    /// </summary>
    public void Deconstruct(out T width, out T height, out T depth)
    {
        width = Width;
        height = Height;
        depth = Depth;
    }

    #endregion

    public override string ToString() => $"{Width} x {Height} x {Depth}";
}
