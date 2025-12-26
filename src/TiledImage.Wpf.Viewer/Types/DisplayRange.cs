namespace TiledImage.Wpf.Viewer.Types;

/// <summary>
/// Represents a display range for LUT (Look-Up Table) mapping.
/// Used to control brightness/contrast by mapping pixel values to display values.
/// </summary>
/// <param name="Min">Minimum value of the display range (maps to 0/black)</param>
/// <param name="Max">Maximum value of the display range (maps to 255/white)</param>
public readonly record struct DisplayRange(double Min, double Max)
{
    /// <summary>
    /// Width of the display range (contrast control).
    /// </summary>
    public double Width => Max - Min;

    /// <summary>
    /// Center of the display range (brightness control).
    /// </summary>
    public double Center => (Min + Max) / 2;

    /// <summary>
    /// Creates a DisplayRange from window/level parameters (DICOM style).
    /// </summary>
    /// <param name="center">Window center (level)</param>
    /// <param name="width">Window width</param>
    public static DisplayRange FromWindowLevel(double center, double width)
        => new(center - width / 2, center + width / 2);

    /// <summary>
    /// Default range for 8-bit images (0-255).
    /// </summary>
    public static DisplayRange Default8Bit => new(0, 255);

    /// <summary>
    /// Default range for 16-bit images (0-65535).
    /// </summary>
    public static DisplayRange Default16Bit => new(0, 65535);

    /// <summary>
    /// Checks if this range represents the full range for 8-bit images.
    /// </summary>
    public bool IsDefault8Bit => Min == 0 && Max == 255;

    /// <summary>
    /// Checks if this range represents the full range for 16-bit images.
    /// </summary>
    public bool IsDefault16Bit => Min == 0 && Max == 65535;
}
