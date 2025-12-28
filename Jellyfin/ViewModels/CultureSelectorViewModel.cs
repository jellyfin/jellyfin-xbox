using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jellyfin.Core;
using Jellyfin.Helpers.Localization;
using Windows.Globalization;

namespace Jellyfin.ViewModels;

/// <summary>
/// Represents a view model that manages culture selection and provides commands and data for switching application
/// cultures at runtime.
/// </summary>
/// <remarks>This view model exposes a list of supported cultures and a command to change the application's
/// current culture. It is typically used in user interfaces that allow users to select their preferred language or
/// regional settings. The available cultures are determined by scanning for resource files present alongside the
/// application's executable. Changing the culture may affect UI language, formatting, and other culture-specific
/// behaviors throughout the application.</remarks>
public class CultureSelectorViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CultureSelectorViewModel"/> class.
    /// </summary>
    /// <remarks>This constructor populates the SupportedCultures collection with the available cultures and
    /// initializes the SetCultureCommand. Use this constructor to create a view model instance ready for culture
    /// selection operations.</remarks>
    public CultureSelectorViewModel()
    {
        SetCultureCommand = new RelayCommand(SetCultureExecute, CanSetCultureExecute);
        SupportedCultures = new();

        foreach (var item in ResourceManagerStringLocalizerFactory.GetCultures())
        {
            var culture = CultureInfo.CreateSpecificCulture(item);
            SupportedCultures.Add(CultureInfo.GetCultureInfo(item));
        }

        SelectedCulture = SupportedCultures.Contains(CultureInfo.CurrentUICulture) ? CultureInfo.CurrentUICulture : SupportedCultures.First();
    }

    /// <summary>
    /// Occurs when the application's current culture changes.
    /// </summary>
    /// <remarks>Subscribers are notified with the new culture information when the culture is updated. This
    /// event can be used to update UI elements or perform other actions that depend on the application's culture
    /// settings.</remarks>
    public static event EventHandler<CultureInfo> CultureChanged;

    /// <summary>
    /// Gets or sets the command that changes the application's culture or language at runtime.
    /// </summary>
    /// <remarks>This command can be bound to user interface elements to allow users to switch languages or
    /// regional settings dynamically. The specific behavior depends on the implementation of the command and the
    /// application's localization strategy.</remarks>
    public ICommand SetCultureCommand { get; set; }

    /// <summary>
    /// Gets or sets the culture information currently selected for formatting and localization operations.
    /// </summary>
    public CultureInfo SelectedCulture
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (SetCultureCommand.CanExecute(null))
                {
                    SetCultureCommand.Execute(null);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the collection of cultures supported by the application.
    /// </summary>
    /// <remarks>The collection determines which cultures are available for localization or formatting within
    /// the application. Modifying this collection at runtime will affect culture-dependent features that rely on the
    /// supported cultures list.</remarks>
    public ObservableCollection<CultureInfo> SupportedCultures { get; set; }

    private bool CanSetCultureExecute()
    {
        return SelectedCulture != CultureInfo.CurrentUICulture;
    }

    private void SetCultureExecute()
    {
        CultureInfo.CurrentCulture = SelectedCulture;
        CultureInfo.CurrentUICulture = SelectedCulture;

        Thread.CurrentThread.CurrentCulture = SelectedCulture;
        Thread.CurrentThread.CurrentUICulture = SelectedCulture;
        ApplicationLanguages.PrimaryLanguageOverride = SelectedCulture.Name;

        Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
        Windows.ApplicationModel.Resources.Core.ResourceContext.GetForViewIndependentUse().Reset();

        var handler = CultureChanged;
        if (handler != null)
        {
            handler(this, SelectedCulture);
        }
    }
}
