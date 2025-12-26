using System.Numerics;

namespace Geometry.Primitives;

/// <summary>
/// Extension methods for floating-point Point2 and Point3 types.
/// </summary>
public static class FloatingPointExtensions
{
    #region Point2<T> Extensions

    /// <summary>
    /// Returns the length (magnitude) of this vector.
    /// </summary>
    public static T Length<T>(this Point2<T> point)
        where T : struct, IFloatingPointIeee754<T>
        => T.Sqrt(point.X * point.X + point.Y * point.Y);

    /// <summary>
    /// Returns the distance to another point.
    /// </summary>
    public static T DistanceTo<T>(this Point2<T> point, Point2<T> other)
        where T : struct, IFloatingPointIeee754<T>
    {
        var dx = other.X - point.X;
        var dy = other.Y - point.Y;
        return T.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Returns a normalized (unit length) vector.
    /// </summary>
    public static Point2<T> Normalize<T>(this Point2<T> point)
        where T : struct, IFloatingPointIeee754<T>
    {
        var len = point.Length();
        if (len == T.Zero)
            return Point2<T>.Zero;
        return new(point.X / len, point.Y / len);
    }

    /// <summary>
    /// Rotates this point around a center point by the given angle in radians.
    /// </summary>
    public static Point2<T> RotateAround<T>(this Point2<T> point, Point2<T> center, T radians)
        where T : struct, IFloatingPointIeee754<T>
    {
        var cos = T.Cos(radians);
        var sin = T.Sin(radians);

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;

        return new(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    /// <summary>
    /// Rotates this point around a center point by the given angle in degrees.
    /// </summary>
    public static Point2<T> RotateAroundDegrees<T>(this Point2<T> point, Point2<T> center, T degrees)
        where T : struct, IFloatingPointIeee754<T>
    {
        var radians = degrees * T.Pi / T.CreateChecked(180);
        return RotateAround(point, center, radians);
    }

    /// <summary>
    /// Rotates this point around the origin by the given angle in radians.
    /// </summary>
    public static Point2<T> Rotate<T>(this Point2<T> point, T radians)
        where T : struct, IFloatingPointIeee754<T>
    {
        var cos = T.Cos(radians);
        var sin = T.Sin(radians);

        return new(
            point.X * cos - point.Y * sin,
            point.X * sin + point.Y * cos);
    }

    /// <summary>
    /// Returns the angle of this vector in radians (from positive X axis).
    /// </summary>
    public static T Angle<T>(this Point2<T> point)
        where T : struct, IFloatingPointIeee754<T>
        => T.Atan2(point.Y, point.X);

    /// <summary>
    /// Returns the angle between this vector and another vector in radians.
    /// </summary>
    public static T AngleTo<T>(this Point2<T> point, Point2<T> other)
        where T : struct, IFloatingPointIeee754<T>
        => T.Atan2(other.Y - point.Y, other.X - point.X);

    /// <summary>
    /// Linear interpolation between two points.
    /// </summary>
    public static Point2<T> Lerp<T>(this Point2<T> a, Point2<T> b, T t)
        where T : struct, IFloatingPointIeee754<T>
    {
        var oneMinusT = T.One - t;
        return new(a.X * oneMinusT + b.X * t, a.Y * oneMinusT + b.Y * t);
    }

    #endregion

    #region Point3<T> Extensions

    /// <summary>
    /// Returns the length (magnitude) of this vector.
    /// </summary>
    public static T Length<T>(this Point3<T> point)
        where T : struct, IFloatingPointIeee754<T>
        => T.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);

    /// <summary>
    /// Returns the distance to another point.
    /// </summary>
    public static T DistanceTo<T>(this Point3<T> point, Point3<T> other)
        where T : struct, IFloatingPointIeee754<T>
    {
        var dx = other.X - point.X;
        var dy = other.Y - point.Y;
        var dz = other.Z - point.Z;
        return T.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Returns a normalized (unit length) vector.
    /// </summary>
    public static Point3<T> Normalize<T>(this Point3<T> point)
        where T : struct, IFloatingPointIeee754<T>
    {
        var len = point.Length();
        if (len == T.Zero)
            return Point3<T>.Zero;
        return new(point.X / len, point.Y / len, point.Z / len);
    }

    /// <summary>
    /// Linear interpolation between two points.
    /// </summary>
    public static Point3<T> Lerp<T>(this Point3<T> a, Point3<T> b, T t)
        where T : struct, IFloatingPointIeee754<T>
    {
        var oneMinusT = T.One - t;
        return new(
            a.X * oneMinusT + b.X * t,
            a.Y * oneMinusT + b.Y * t,
            a.Z * oneMinusT + b.Z * t);
    }

    #endregion
}
