namespace Geometry.Shapes.Types;

/// <summary>
/// UI-independent style definition for shapes.
/// Colors are stored as ARGB uint values.
/// </summary>
public readonly struct ShapeStyle : IEquatable<ShapeStyle>
{
    /// <summary>
    /// Stroke (outline) color in ARGB format.
    /// </summary>
    public uint StrokeColor { get; }

    /// <summary>
    /// Fill color in ARGB format.
    /// </summary>
    public uint FillColor { get; }

    /// <summary>
    /// Stroke thickness in pixels.
    /// </summary>
    public double StrokeThickness { get; }

    public ShapeStyle(uint strokeColor, uint fillColor, double strokeThickness)
    {
        StrokeColor = strokeColor;
        FillColor = fillColor;
        StrokeThickness = strokeThickness;
    }

    /// <summary>
    /// Default style: Black stroke, transparent fill, 1px thickness.
    /// </summary>
    public static ShapeStyle Default => new(0xFF000000, 0x00000000, 1.0);

    /// <summary>
    /// Selected shape style: Blue stroke, semi-transparent blue fill.
    /// </summary>
    public static ShapeStyle Selected => new(0xFF0078D4, 0x400078D4, 2.0);

    /// <summary>
    /// Creates a style from ARGB components.
    /// </summary>
    public static ShapeStyle FromArgb(byte strokeA, byte strokeR, byte strokeG, byte strokeB,
        byte fillA, byte fillR, byte fillG, byte fillB, double strokeThickness)
    {
        var stroke = ((uint)strokeA << 24) | ((uint)strokeR << 16) | ((uint)strokeG << 8) | strokeB;
        var fill = ((uint)fillA << 24) | ((uint)fillR << 16) | ((uint)fillG << 8) | fillB;
        return new ShapeStyle(stroke, fill, strokeThickness);
    }

    /// <summary>
    /// Creates a style with only stroke (no fill).
    /// </summary>
    public static ShapeStyle StrokeOnly(uint strokeColor, double strokeThickness = 1.0)
        => new(strokeColor, 0x00000000, strokeThickness);

    /// <summary>
    /// Extracts ARGB components from a color value.
    /// </summary>
    public static (byte A, byte R, byte G, byte B) ToArgb(uint color)
        => ((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color);

    public ShapeStyle WithStrokeColor(uint color) => new(color, FillColor, StrokeThickness);
    public ShapeStyle WithFillColor(uint color) => new(StrokeColor, color, StrokeThickness);
    public ShapeStyle WithStrokeThickness(double thickness) => new(StrokeColor, FillColor, thickness);

    public bool Equals(ShapeStyle other)
        => StrokeColor == other.StrokeColor && FillColor == other.FillColor &&
           StrokeThickness.Equals(other.StrokeThickness);

    public override bool Equals(object? obj) => obj is ShapeStyle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(StrokeColor, FillColor, StrokeThickness);
    public static bool operator ==(ShapeStyle left, ShapeStyle right) => left.Equals(right);
    public static bool operator !=(ShapeStyle left, ShapeStyle right) => !left.Equals(right);
}
