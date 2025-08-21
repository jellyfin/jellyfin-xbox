using System;
using Windows.UI.Xaml.Data;

namespace Jellyfin.Converter;

/// <summary>
/// Gets a boolean value indicating whether the value is null.
/// </summary>
public class NullToBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is null;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new Exception("ConvertBack is not supported for NullToBooleanConverter.");
    }
}
