using TiledImage.Wpf.Viewer.Types;

namespace TiledImage.Wpf.Viewer.LUT;

/// <summary>
/// Pre-computed lookup table for fast pixel value mapping.
/// Converts input pixel values to 8-bit display values based on DisplayRange.
/// </summary>
public sealed class LookupTable
{
    private readonly byte[] _table;

    /// <summary>
    /// Maximum input value this LUT can handle (255 for 8-bit, 65535 for 16-bit).
    /// </summary>
    public int MaxInputValue { get; }

    /// <summary>
    /// The display range used to create this LUT.
    /// </summary>
    public DisplayRange Range { get; }

    /// <summary>
    /// Whether this LUT is an identity mapping (no transformation).
    /// </summary>
    public bool IsIdentity { get; }

    private LookupTable(byte[] table, int maxInputValue, DisplayRange range, bool isIdentity)
    {
        _table = table;
        MaxInputValue = maxInputValue;
        Range = range;
        IsIdentity = isIdentity;
    }

    /// <summary>
    /// Creates a lookup table for 8-bit input values.
    /// </summary>
    /// <param name="range">Display range for mapping</param>
    public static LookupTable Create8Bit(DisplayRange range)
    {
        const int tableSize = 256;
        var table = new byte[tableSize];
        bool isIdentity = range.IsDefault8Bit;

        if (isIdentity)
        {
            // Identity mapping: output = input
            for (int i = 0; i < tableSize; i++)
            {
                table[i] = (byte)i;
            }
        }
        else
        {
            double scale = 255.0 / (range.Max - range.Min);
            for (int i = 0; i < tableSize; i++)
            {
                double normalized = (i - range.Min) * scale;
                table[i] = (byte)Math.Clamp(normalized, 0, 255);
            }
        }

        return new LookupTable(table, 255, range, isIdentity);
    }

    /// <summary>
    /// Creates a lookup table for 16-bit input values.
    /// </summary>
    /// <param name="range">Display range for mapping</param>
    public static LookupTable Create16Bit(DisplayRange range)
    {
        const int tableSize = 65536;
        var table = new byte[tableSize];
        bool isIdentity = range.IsDefault16Bit;

        if (isIdentity)
        {
            // Default 16-bit to 8-bit: use high byte
            for (int i = 0; i < tableSize; i++)
            {
                table[i] = (byte)(i >> 8);
            }
        }
        else
        {
            double scale = 255.0 / (range.Max - range.Min);
            for (int i = 0; i < tableSize; i++)
            {
                double normalized = (i - range.Min) * scale;
                table[i] = (byte)Math.Clamp(normalized, 0, 255);
            }
        }

        return new LookupTable(table, 65535, range, isIdentity);
    }

    /// <summary>
    /// Applies the lookup table to a single value.
    /// </summary>
    /// <param name="value">Input pixel value (0-255 for 8-bit, 0-65535 for 16-bit)</param>
    /// <returns>Mapped 8-bit display value</returns>
    public byte Apply(int value)
    {
        // Clamp to valid range
        if (value < 0) return 0;
        if (value > MaxInputValue) return 255;
        return _table[value];
    }

    /// <summary>
    /// Applies the lookup table to a single byte value (8-bit).
    /// </summary>
    public byte Apply(byte value) => _table[value];

    /// <summary>
    /// Applies the lookup table to a single ushort value (16-bit).
    /// </summary>
    public byte Apply(ushort value)
    {
        if (MaxInputValue == 255)
        {
            // 8-bit LUT, take high byte of 16-bit value
            return _table[value >> 8];
        }
        return _table[value];
    }
}
