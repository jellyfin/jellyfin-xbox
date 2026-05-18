using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
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
        (DataContext as SettingsViewModel).CloseAction = () =>
        {
            ParentPopup.IsOpen = false;
            Window.Current.SizeChanged -= Current_SizeChanged;
        };
        Window.Current.SizeChanged += Current_SizeChanged;
    }

    /// <summary>
    /// Gets or sets the action to perform when navigating to the main page.
    /// </summary>
    public Popup ParentPopup { get; set; }

    private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
    {
        this.Width = e.Size.Width;
        this.Height = e.Size.Height;
        ParentPopup.Width = e.Size.Width;
        ParentPopup.Height = e.Size.Height;
    }
}
