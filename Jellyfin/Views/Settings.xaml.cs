using Jellyfin.Core;
using Windows.Graphics.Display.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Views;

/// <summary>
/// Settings page.
/// </summary>
public sealed partial class Settings : Page
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Settings"/> class.
    /// </summary>
    public Settings()
    {
        InitializeComponent();
        btnSave.Click += BtnSave_Click;
        btnAbort.Click += BtnAbort_Click;

        HdmiDisplayInformation hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        checkBoxAutoRefreshRate.IsEnabled = hdmiDisplayInformation != null;
        checkBoxAutoRefreshRate.IsChecked = Central.Settings.AutoRefreshRate;
        checkBoxAutoResolution.IsEnabled = hdmiDisplayInformation != null;
        checkBoxAutoResolution.IsChecked = Central.Settings.AutoResolution;
    }

    private void BtnAbort_Click(object sender, RoutedEventArgs e)
    {
        NavigateToMainPage();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        NavigateToMainPage();
    }

    private void SaveSettings()
    {
        if (checkBoxAutoRefreshRate.IsEnabled)
        {
            Central.Settings.AutoRefreshRate = checkBoxAutoRefreshRate.IsChecked ?? false;
        }

        if (checkBoxAutoResolution.IsEnabled)
        {
            Central.Settings.AutoResolution = checkBoxAutoResolution.IsChecked ?? false;
        }
    }

    private static void NavigateToMainPage()
    {
        (Window.Current.Content as Frame).Navigate(typeof(MainPage));
    }
}
