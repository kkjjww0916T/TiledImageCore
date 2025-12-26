using Geometry.Primitives;
using SkiaSharp;
using TiledImage.Transforms;
using TiledImage.Transforms.Operations;

namespace TiledImage.Wpf.Mapping;

/// <summary>
/// Extension methods for converting between LargeImage types and SkiaSharp types.
/// </summary>
public static class SkiaExtensions
{
    /// <summary>
    /// Convert Matrix3x3 to SKMatrix.
    /// </summary>
    public static SKMatrix ToSKMatrix(this Matrix3x3 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13,
        matrix.M21, matrix.M22, matrix.M23,
        matrix.M31, matrix.M32, matrix.M33);

    /// <summary>
    /// Convert SKPoint to Point2F.
    /// </summary>
    public static Point2F ToPoint2F(this SKPoint point) => new(point.X, point.Y);

    /// <summary>
    /// Convert Point2F to SKPoint.
    /// </summary>
    public static SKPoint ToSKPoint(this Point2F point) => new(point.X, point.Y);

    /// <summary>
    /// Convert SKPoint array to Point2F array.
    /// </summary>
    public static Point2F[] ToPoint2FArray(this SKPoint[] points)
    {
        var result = new Point2F[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            result[i] = new Point2F(points[i].X, points[i].Y);
        }
        return result;
    }

    /// <summary>
    /// Compute SKMatrix homography from SKPoint arrays.
    /// Uses PerspectiveTransform.ComputeHomography internally.
    /// </summary>
    public static SKMatrix ComputeHomography(SKPoint[] src, SKPoint[] dst)
    {
        return PerspectiveTransform.ComputeHomography(
            src.ToPoint2FArray(),
            dst.ToPoint2FArray()
        ).ToSKMatrix();
    }
}
