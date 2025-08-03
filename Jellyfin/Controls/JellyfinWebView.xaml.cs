using System;
using Jellyfin.Core;
using Jellyfin.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Controls;

/// <summary>
/// Represents a custom web view control for interacting with a Jellyfin server.
/// </summary>
public sealed partial class JellyfinWebView : UserControl, IDisposable
{
    private readonly GamepadManager _gamepadManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinWebView"/> class.
    /// </summary>
    public JellyfinWebView()
    {
        InitializeComponent();

        // Set WebView source
        WView.Source = new Uri(Central.Settings.JellyfinServer);

        WView.CoreWebView2Initialized += WView_CoreWebView2Initialized;
        WView.NavigationCompleted += JellyfinWebView_NavigationCompleted;
        SystemNavigationManager.GetForCurrentView().BackRequested += Back_BackRequested;

        // Initialize GamepadManager
        _gamepadManager = new GamepadManager();
        _gamepadManager.OnBackPressed += HandleGamepadBackPress;
    }

    private void HandleGamepadBackPress()
    {
        if (WView.CanGoBack)
        {
            WView.GoBack();
        }
    }

    private void WView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        WView.CoreWebView2.ContainsFullScreenElementChanged += JellyfinWebView_ContainsFullScreenElementChanged;
    }

    private void Back_BackRequested(object sender, BackRequestedEventArgs args)
    {
        if (WView.CanGoBack)
        {
            WView.GoBack();
        }

        args.Handled = true;
    }

    private async void JellyfinWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            CoreWebView2WebErrorStatus errorStatus = args.WebErrorStatus;
            MessageDialog md = new MessageDialog($"Navigation failed: {errorStatus}");
            await md.ShowAsync();
        }

        await WView.ExecuteScriptAsync("navigator.gamepadInputEmulation = 'mouse';");
    }

    private void JellyfinWebView_ContainsFullScreenElementChanged(CoreWebView2 sender, object args)
    {
        ApplicationView appView = ApplicationView.GetForCurrentView();

        if (sender.ContainsFullScreenElement)
        {
            appView.TryEnterFullScreenMode();
            return;
        }

        if (appView.IsFullScreenMode)
        {
            appView.ExitFullScreenMode();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gamepadManager?.Dispose();
    }
}
