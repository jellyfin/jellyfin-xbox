using System;
using Jellyfin.Core;
using Jellyfin.Utils;
using Windows.Graphics.Display.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace Jellyfin.Views;

/// <summary>
/// Settings page.
/// </summary>
public sealed partial class Settings : IDisposable
{
    private IDisposable _navigationHandler;

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

        _navigationHandler = GamepadManager.ObserveBackEvent(ModalPage_BackRequested, -10);
    }

    /// <summary>
    /// Gets or sets the action to perform when navigating to the main page.
    /// </summary>
    public Popup ParentPopup { get; set; }

    private void ModalPage_BackRequested(BackRequestedEventArgs e)
    {
        e.Handled = true;
        NavigateToMainPage();
    }

    private void BtnAbort_Click(object sender, RoutedEventArgs e)
    {
        NavigateToMainPage();
    }

    private void NavigateToMainPage()
    {
        ParentPopup.IsOpen = false;
        _navigationHandler.Dispose();
        _navigationHandler = null;
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

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationHandler?.Dispose();
    }
}
