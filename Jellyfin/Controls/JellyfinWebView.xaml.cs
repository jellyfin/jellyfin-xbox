using Jellyfin.Core;
using Jellyfin.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Controls
{
    public sealed partial class JellyfinWebView : UserControl, IDisposable
    {
        private readonly GamepadManager _gamepadManager;

        public JellyfinWebView()
        {
            this.InitializeComponent();

            // Set WebView source
            WView.Source = new Uri(Central.Settings.JellyfinServer);

            WView.CoreWebView2Initialized += WView_CoreWebView2Initialized;
            WView.NavigationStarting += WView_NavigationStarting;
            WView.NavigationCompleted += JellyfinWebView_NavigationCompleted;
            SystemNavigationManager.GetForCurrentView().BackRequested += Back_BackRequested;

            // Initialize GamepadManager
            _gamepadManager = new GamepadManager();
        }

        private void WView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            // Workaround to fix focus issues with gamepad not focusing window
            BtnFocusStealer.Visibility = Visibility.Visible;
            BtnFocusStealer.Focus(FocusState.Programmatic);
        }

        private void WView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            // Set useragent to Xbox and WebView2 since WebView2 only sets these in Sec-CA-UA, which isn't available over HTTP.
            WView.CoreWebView2.Settings.UserAgent += " WebView2 " + Utils.AppUtils.GetDeviceFormFactorType().ToString();
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

            // Refocus WebView after having BtnFocusStealer steal it.
            // To work around WebView2 not focusing web content.
            WView.Focus(FocusState.Programmatic);
            BtnFocusStealer.Visibility = Visibility.Collapsed;
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

        public void Dispose()
        {
            _gamepadManager?.Dispose();
        }
    }
}