using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TiledImage.Demo.ViewModels;

namespace TiledImage.Demo;

public partial class TransformDemoWindow : Window
{
    private readonly TransformDemoViewModel _viewModel = new();

    public TransformDemoWindow()
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
/// Converts null to Visible and non-null to Collapsed.
/// Used to show placeholder text when no image is loaded.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
