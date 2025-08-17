using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Markup;

namespace Jellyfin.Converter;

/// <summary>
/// Uses a list of converters which are used to convert a value in sequence.
/// </summary>
[DefaultProperty(nameof(Converters))]
[ContentProperty(Name = nameof(Converters))]
public class ChainConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the list of converters.
    /// </summary>
    public List<IValueConverter> Converters { get; set; } = new List<IValueConverter>();

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (!Converters.Any())
        {
            return value;
        }

        object result = value;
        foreach (var converter in Converters)
        {
            result = converter.Convert(result, targetType, parameter, language);
        }

        return result;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (!Converters.Any())
        {
            return value;
        }

        object result = value;
        for (int i = Converters.Count - 1; i >= 0; i--)
        {
            result = Converters[i].ConvertBack(result, targetType, parameter, language);
        }

        return result;
    }
}
