using System;
using Windows.UI.Xaml.Data;

namespace Jellyfin.Converter;

/// <summary>
/// Converts a boolean value to an inverse <see cref="Windows.UI.Xaml.Visibility"/> value.
/// </summary>
public class BooleanVisibilityInverseConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? Windows.UI.Xaml.Visibility.Collapsed
            : Windows.UI.Xaml.Visibility.Visible;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Windows.UI.Xaml.Visibility.Collapsed;
    }
}
