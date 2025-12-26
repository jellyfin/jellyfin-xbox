using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Resources.Localisations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Windows.UI.Xaml.Markup;

namespace Jellyfin.Helpers.MarkupExtensions;

[ContentProperty(Name = nameof(Key))]
[MarkupExtensionReturnType(ReturnType = typeof(string))]

internal class LocalizeExtension : MarkupExtension
{
    private IStringLocalizer _localizer;

    public LocalizeExtension()
    {
        if (App.Current is App) // check to avoid design time errors
        {
            _localizer = App.Current.Services.GetRequiredService<IStringLocalizer<Strings>>();
        }
    }

    public LocalizeExtension(string key) : this()
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue()
    {
        if (App.Current is null || _localizer is null) // check to avoid design time errors
        {
            return Key;
        }

        try
        {
            string localizedText = _localizer[Key];
            return localizedText;
        }
        catch (Exception)
        {
            return Key;
        }
    }
}
