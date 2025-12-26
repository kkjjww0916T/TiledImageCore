using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TiledImage.Demo.ViewModels;

namespace TiledImage.Demo;

public partial class DrawingWindow : Window
{
    private readonly DrawingViewerViewModel _viewModel = new();

    public DrawingWindow()
    {
        DataContext = _viewModel;
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}

/// <summary>
/// Converts depth value to visibility.
/// Returns Visible if depth > 1, otherwise Collapsed.
/// </summary>
public class DepthToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long depth)
        {
            return depth > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a Point to a formatted string.
/// </summary>
public class PointToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Point point)
        {
            return $"({point.X:F0}, {point.Y:F0})";
        }
        return "(-, -)";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a zoom value (double) to a percentage string.
/// </summary>
public class ZoomToPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double zoom)
        {
            return $"{zoom * 100:F0}%";
        }
        return "100%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
