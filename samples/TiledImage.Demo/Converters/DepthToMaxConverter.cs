using System.Globalization;
using System.Windows.Data;

namespace TiledImage.Demo.Converters;

/// <summary>
/// Converts ImageDepth to slider maximum value (depth - 1).
/// Z-index is 0-based, so max index = depth - 1.
/// </summary>
public class DepthToMaxConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long depth && depth > 0)
        {
            return (double)(depth - 1);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
