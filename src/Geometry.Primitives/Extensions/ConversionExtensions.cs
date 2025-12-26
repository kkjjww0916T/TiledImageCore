using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// Extension methods for type conversion between different numeric types.
/// </summary>
public static class ConversionExtensions
{
    #region Point2 Conversions

    /// <summary>
    /// Casts the point to a different numeric type.
    /// </summary>
    public static Point2<TTarget> Cast<TSource, TTarget>(this Point2<TSource> point)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(TTarget.CreateChecked(point.X), TTarget.CreateChecked(point.Y));

    /// <summary>
    /// Rounds the point to the nearest integer values and returns as long.
    /// </summary>
    public static Point2<long> Round<T>(this Point2<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Round(point.X)), long.CreateChecked(T.Round(point.Y)));

    /// <summary>
    /// Floors the point values and returns as long.
    /// </summary>
    public static Point2<long> Floor<T>(this Point2<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Floor(point.X)), long.CreateChecked(T.Floor(point.Y)));

    /// <summary>
    /// Ceilings the point values and returns as long.
    /// </summary>
    public static Point2<long> Ceiling<T>(this Point2<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Ceiling(point.X)), long.CreateChecked(T.Ceiling(point.Y)));

    /// <summary>
    /// Rounds the point to the nearest integer values and returns as int.
    /// </summary>
    public static Point2<int> RoundToInt<T>(this Point2<T> point)
        where T : struct, IFloatingPoint<T>
        => new(int.CreateChecked(T.Round(point.X)), int.CreateChecked(T.Round(point.Y)));

    #endregion

    #region Point3 Conversions

    /// <summary>
    /// Casts the point to a different numeric type.
    /// </summary>
    public static Point3<TTarget> Cast<TSource, TTarget>(this Point3<TSource> point)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(TTarget.CreateChecked(point.X), TTarget.CreateChecked(point.Y), TTarget.CreateChecked(point.Z));

    /// <summary>
    /// Rounds the point to the nearest integer values and returns as long.
    /// </summary>
    public static Point3<long> Round<T>(this Point3<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Round(point.X)), long.CreateChecked(T.Round(point.Y)), long.CreateChecked(T.Round(point.Z)));

    /// <summary>
    /// Floors the point values and returns as long.
    /// </summary>
    public static Point3<long> Floor<T>(this Point3<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Floor(point.X)), long.CreateChecked(T.Floor(point.Y)), long.CreateChecked(T.Floor(point.Z)));

    /// <summary>
    /// Ceilings the point values and returns as long.
    /// </summary>
    public static Point3<long> Ceiling<T>(this Point3<T> point)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Ceiling(point.X)), long.CreateChecked(T.Ceiling(point.Y)), long.CreateChecked(T.Ceiling(point.Z)));

    #endregion

    #region Size2 Conversions

    /// <summary>
    /// Casts the size to a different numeric type.
    /// </summary>
    public static Size2<TTarget> Cast<TSource, TTarget>(this Size2<TSource> size)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(TTarget.CreateChecked(size.Width), TTarget.CreateChecked(size.Height));

    /// <summary>
    /// Rounds the size to the nearest integer values and returns as long.
    /// </summary>
    public static Size2<long> Round<T>(this Size2<T> size)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Round(size.Width)), long.CreateChecked(T.Round(size.Height)));

    /// <summary>
    /// Ceilings the size values and returns as long.
    /// </summary>
    public static Size2<long> Ceiling<T>(this Size2<T> size)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Ceiling(size.Width)), long.CreateChecked(T.Ceiling(size.Height)));

    #endregion

    #region Size3 Conversions

    /// <summary>
    /// Casts the size to a different numeric type.
    /// </summary>
    public static Size3<TTarget> Cast<TSource, TTarget>(this Size3<TSource> size)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(TTarget.CreateChecked(size.Width), TTarget.CreateChecked(size.Height), TTarget.CreateChecked(size.Depth));

    /// <summary>
    /// Rounds the size to the nearest integer values and returns as long.
    /// </summary>
    public static Size3<long> Round<T>(this Size3<T> size)
        where T : struct, IFloatingPoint<T>
        => new(long.CreateChecked(T.Round(size.Width)), long.CreateChecked(T.Round(size.Height)), long.CreateChecked(T.Round(size.Depth)));

    #endregion

    #region Rect2 Conversions

    /// <summary>
    /// Casts the rect to a different numeric type.
    /// </summary>
    public static Rect2<TTarget> Cast<TSource, TTarget>(this Rect2<TSource> rect)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(
            TTarget.CreateChecked(rect.X),
            TTarget.CreateChecked(rect.Y),
            TTarget.CreateChecked(rect.Width),
            TTarget.CreateChecked(rect.Height));

    /// <summary>
    /// Rounds the rect to the nearest integer values and returns as long.
    /// </summary>
    public static Rect2<long> Round<T>(this Rect2<T> rect)
        where T : struct, IFloatingPoint<T>
        => new(
            long.CreateChecked(T.Round(rect.X)),
            long.CreateChecked(T.Round(rect.Y)),
            long.CreateChecked(T.Round(rect.Width)),
            long.CreateChecked(T.Round(rect.Height)));

    /// <summary>
    /// Floors the location and ceilings the size for pixel-perfect coverage.
    /// </summary>
    public static Rect2<long> RoundOutward<T>(this Rect2<T> rect)
        where T : struct, IFloatingPoint<T>
    {
        var left = long.CreateChecked(T.Floor(rect.X));
        var top = long.CreateChecked(T.Floor(rect.Y));
        var right = long.CreateChecked(T.Ceiling(rect.Right));
        var bottom = long.CreateChecked(T.Ceiling(rect.Bottom));
        return new(left, top, right - left, bottom - top);
    }

    #endregion

    #region Box3 Conversions

    /// <summary>
    /// Casts the box to a different numeric type.
    /// </summary>
    public static Box3<TTarget> Cast<TSource, TTarget>(this Box3<TSource> box)
        where TSource : struct, INumber<TSource>
        where TTarget : struct, INumber<TTarget>
        => new(
            TTarget.CreateChecked(box.X),
            TTarget.CreateChecked(box.Y),
            TTarget.CreateChecked(box.Z),
            TTarget.CreateChecked(box.Width),
            TTarget.CreateChecked(box.Height),
            TTarget.CreateChecked(box.Depth));

    /// <summary>
    /// Rounds the box to the nearest integer values and returns as long.
    /// </summary>
    public static Box3<long> Round<T>(this Box3<T> box)
        where T : struct, IFloatingPoint<T>
        => new(
            long.CreateChecked(T.Round(box.X)),
            long.CreateChecked(T.Round(box.Y)),
            long.CreateChecked(T.Round(box.Z)),
            long.CreateChecked(T.Round(box.Width)),
            long.CreateChecked(T.Round(box.Height)),
            long.CreateChecked(T.Round(box.Depth)));

    #endregion
}
