using System;
using Windows.UI.Xaml.Data;

namespace Jellyfin.Converter;

/// <summary>
/// Converts a boolean value to a <see cref="Windows.UI.Xaml.Visibility"/> value.
/// </summary>
public class BooleanVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? Windows.UI.Xaml.Visibility.Visible
            : Windows.UI.Xaml.Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Windows.UI.Xaml.Visibility.Visible;
    }
}
