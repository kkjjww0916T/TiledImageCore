using System.Collections.Concurrent;
using System.Windows.Media;
using Geometry.Shapes.Types;

namespace TiledImage.Wpf.Drawing;

/// <summary>
/// Converts ShapeStyle to WPF Brush/Pen objects.
/// Uses caching for better performance.
/// </summary>
public static class ShapeStyleConverter
{
    // Cache for brushes (keyed by ARGB color)
    private static readonly ConcurrentDictionary<uint, SolidColorBrush> _brushCache = new();

    public static SolidColorBrush ToStrokeBrush(this ShapeStyle style)
    {
        return GetOrCreateBrush(style.StrokeColor);
    }

    public static SolidColorBrush ToFillBrush(this ShapeStyle style)
    {
        return GetOrCreateBrush(style.FillColor);
    }

    private static SolidColorBrush GetOrCreateBrush(uint argb)
    {
        return _brushCache.GetOrAdd(argb, color =>
        {
            var (a, r, g, b) = ShapeStyle.ToArgb(color);
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze(); // Freeze for better performance
            return brush;
        });
    }

    public static Pen ToPen(this ShapeStyle style, double zoomScale = 1.0)
    {
        var brush = style.ToStrokeBrush();
        // Keep stroke thickness constant on screen regardless of zoom
        // Since shapes are already rendered in screen coordinates (after ImageToScreen transform),
        // the pen thickness should remain constant without zoom adjustment
        var thickness = style.StrokeThickness;
        var pen = new Pen(brush, Math.Max(0.5, thickness));
        pen.Freeze();
        return pen;
    }

    public static Color ToColor(uint argb)
    {
        var (a, r, g, b) = ShapeStyle.ToArgb(argb);
        return Color.FromArgb(a, r, g, b);
    }

    public static uint FromColor(Color color)
    {
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }
}
