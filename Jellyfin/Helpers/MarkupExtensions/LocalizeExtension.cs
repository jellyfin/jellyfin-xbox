using System;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
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
            _localizer = App.Current.Services.GetRequiredService<IStringLocalizer<Translations>>();
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
            var binding = new Binding()
            {
                Source = new LocBindingSource(_localizer, Key),
                Path = new PropertyPath(nameof(LocBindingSource.Text)),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
            return binding;
        }
        catch (Exception)
        {
            return Key;
        }
    }

    private class LocBindingSource : ObservableObject
    {
        private readonly IStringLocalizer _localizer;
        private readonly string _key;

        public LocBindingSource(IStringLocalizer localizer, string key)
        {
            _localizer = localizer;
            _key = key;

            var weakPropertyChangedListener = new CommunityToolkit.WinUI.Helpers.WeakEventListener<LocBindingSource, object, CultureInfo>(this)
            {
                OnEventAction = static (instance, source, eventArgs) => instance.OnCultureChanged(source, eventArgs),
                OnDetachAction = (weakEventListener) => CultureSelectorViewModel.CultureChanged -= weakEventListener.OnEvent // Use Local References Only
            };

            CultureSelectorViewModel.CultureChanged += weakPropertyChangedListener.OnEvent;
        }

        public string Text
        {
            get
            {
                var text = _localizer.GetString(_key);
                return text;
            }
        }

        private void OnCultureChanged(object sender, CultureInfo e)
        {
            OnPropertyChanged(nameof(Text));
        }
    }
}
