using System;
using Windows.UI.Xaml.Data;

namespace Jellyfin.Converter;

/// <summary>
/// Gets a boolean value indicating whether the value is null.
/// </summary>
public class BooleanInverseConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is false;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is true;
    }
}
