using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls.Primitives;

namespace Jellyfin.Views;

/// <summary>
/// Settings page.
/// </summary>
public sealed partial class Settings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Settings"/> class.
    /// </summary>
    public Settings()
    {
        InitializeComponent();

        DataContext = App.Current.Services.GetRequiredService<SettingsViewModel>();
        (DataContext as SettingsViewModel).CloseAction = () => ParentPopup.IsOpen = false;
    }

    /// <summary>
    /// Gets or sets the action to perform when navigating to the main page.
    /// </summary>
    public Popup ParentPopup { get; set; }
}
