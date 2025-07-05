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
        private readonly WebView2 _wView;

        public JellyfinWebView()
        {
            this.InitializeComponent();

            _wView = new WebView2();
            // Set WebView source
            _wView.Source = new Uri(Central.Settings.JellyfinServer);

            _wView.CoreWebView2Initialized += WView_CoreWebView2Initialized;
            _wView.NavigationCompleted += JellyfinWebView_NavigationCompleted;
        }

        private void WView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            // Must wait for CoreWebView2 to be initialized or the WebView2 would be unfocusable.
            this.Content = _wView;
            _wView.Focus(FocusState.Programmatic);

            // Set useragent to Xbox and WebView2 since WebView2 only sets these in Sec-CA-UA, which isn't available over HTTP.
            _wView.CoreWebView2.Settings.UserAgent += " WebView2 " + Utils.AppUtils.GetDeviceFormFactorType().ToString();
           
            _wView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false; // Disable autofill on Xbox as it puts down the virtual keyboard.
            _wView.CoreWebView2.ContainsFullScreenElementChanged += JellyfinWebView_ContainsFullScreenElementChanged;
        }

        private async void JellyfinWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                CoreWebView2WebErrorStatus errorStatus = args.WebErrorStatus;
                MessageDialog md = new MessageDialog($"Navigation failed: {errorStatus}");
                await md.ShowAsync();
            }
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

        }
    }
}