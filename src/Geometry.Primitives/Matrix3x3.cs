namespace Geometry.Primitives;

/// <summary>
/// Platform-independent 3x3 transformation matrix for 2D graphics.
/// Layout:
/// | ScaleX  SkewX   TransX |   | M11 M12 M13 |
/// | SkewY   ScaleY  TransY | = | M21 M22 M23 |
/// | Persp0  Persp1  Persp2 |   | M31 M32 M33 |
/// </summary>
public struct Matrix3x3 : IEquatable<Matrix3x3>
{
    public float M11, M12, M13;  // ScaleX, SkewX, TransX
    public float M21, M22, M23;  // SkewY, ScaleY, TransY
    public float M31, M32, M33;  // Persp0, Persp1, Persp2

    public Matrix3x3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
    {
        M11 = m11; M12 = m12; M13 = m13;
        M21 = m21; M22 = m22; M23 = m23;
        M31 = m31; M32 = m32; M33 = m33;
    }

    public static Matrix3x3 Identity => new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);

    /// <summary>
    /// Check if this matrix is approximately equal to the identity matrix.
    /// </summary>
    public readonly bool IsIdentity
    {
        get
        {
            const float epsilon = 1e-6f;
            return MathF.Abs(M11 - 1) < epsilon && MathF.Abs(M12) < epsilon && MathF.Abs(M13) < epsilon &&
                   MathF.Abs(M21) < epsilon && MathF.Abs(M22 - 1) < epsilon && MathF.Abs(M23) < epsilon &&
                   MathF.Abs(M31) < epsilon && MathF.Abs(M32) < epsilon && MathF.Abs(M33 - 1) < epsilon;
        }
    }

    /// <summary>
    /// Transform a point using this matrix.
    /// </summary>
    public readonly Point2<float> MapPoint(Point2<float> point)
    {
        float x = point.X;
        float y = point.Y;

        float w = M31 * x + M32 * y + M33;
        if (MathF.Abs(w) < 1e-10f)
            w = 1e-10f;

        float newX = (M11 * x + M12 * y + M13) / w;
        float newY = (M21 * x + M22 * y + M23) / w;

        return new Point2<float>(newX, newY);
    }

    /// <summary>
    /// Try to compute the inverse of this matrix.
    /// </summary>
    public readonly bool TryInvert(out Matrix3x3 inverse)
    {
        float det = M11 * (M22 * M33 - M23 * M32)
                  - M12 * (M21 * M33 - M23 * M31)
                  + M13 * (M21 * M32 - M22 * M31);

        if (MathF.Abs(det) < 1e-10f)
        {
            inverse = Identity;
            return false;
        }

        float invDet = 1f / det;

        inverse = new Matrix3x3(
            (M22 * M33 - M23 * M32) * invDet,
            (M13 * M32 - M12 * M33) * invDet,
            (M12 * M23 - M13 * M22) * invDet,

            (M23 * M31 - M21 * M33) * invDet,
            (M11 * M33 - M13 * M31) * invDet,
            (M13 * M21 - M11 * M23) * invDet,

            (M21 * M32 - M22 * M31) * invDet,
            (M12 * M31 - M11 * M32) * invDet,
            (M11 * M22 - M12 * M21) * invDet
        );

        return true;
    }

    /// <summary>
    /// Multiply two matrices.
    /// </summary>
    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
    {
        return new Matrix3x3(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        );
    }

    public static bool operator ==(Matrix3x3 a, Matrix3x3 b) => a.Equals(b);
    public static bool operator !=(Matrix3x3 a, Matrix3x3 b) => !a.Equals(b);

    public bool Equals(Matrix3x3 other)
    {
        return M11 == other.M11 && M12 == other.M12 && M13 == other.M13 &&
               M21 == other.M21 && M22 == other.M22 && M23 == other.M23 &&
               M31 == other.M31 && M32 == other.M32 && M33 == other.M33;
    }

    public override bool Equals(object? obj) => obj is Matrix3x3 other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(M11); hash.Add(M12); hash.Add(M13);
        hash.Add(M21); hash.Add(M22); hash.Add(M23);
        hash.Add(M31); hash.Add(M32); hash.Add(M33);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Create a translation matrix.
    /// </summary>
    public static Matrix3x3 CreateTranslation(float tx, float ty) => new(
        1, 0, tx,
        0, 1, ty,
        0, 0, 1);

    /// <summary>
    /// Create a scale matrix.
    /// </summary>
    public static Matrix3x3 CreateScale(float sx, float sy) => new(
        sx, 0, 0,
        0, sy, 0,
        0, 0, 1);

    /// <summary>
    /// Create a rotation matrix (angle in radians).
    /// </summary>
    public static Matrix3x3 CreateRotation(float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new(
            cos, -sin, 0,
            sin, cos, 0,
            0, 0, 1);
    }

    /// <summary>
    /// Create a rotation matrix around a specific center point (angle in radians).
    /// The transform order is: translate center to origin → rotate → translate back.
    /// </summary>
    public static Matrix3x3 CreateRotationAt(float radians, float centerX, float centerY)
    {
        // Matrix multiplication is right-to-left:
        // result = T(cx,cy) * R * T(-cx,-cy)
        // Applied to point: first T(-cx,-cy), then R, then T(cx,cy)
        return CreateTranslation(centerX, centerY) *
               CreateRotation(radians) *
               CreateTranslation(-centerX, -centerY);
    }

    /// <summary>
    /// Create a horizontal flip matrix.
    /// </summary>
    public static Matrix3x3 CreateFlipHorizontal(float width) => new(
        -1, 0, width,
        0, 1, 0,
        0, 0, 1);

    /// <summary>
    /// Create a vertical flip matrix.
    /// </summary>
    public static Matrix3x3 CreateFlipVertical(float height) => new(
        1, 0, 0,
        0, -1, height,
        0, 0, 1);
}
