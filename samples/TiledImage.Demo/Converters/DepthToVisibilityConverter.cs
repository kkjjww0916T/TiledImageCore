using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TiledImage.Demo.Converters;

/// <summary>
/// Converts ImageDepth to Visibility.
/// Returns Visible if depth > 1, otherwise Collapsed.
/// </summary>
public class DepthToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long depth)
        {
            return depth > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
