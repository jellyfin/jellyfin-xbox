using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Utils;
using Jellyfin.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Data.Json;
using Windows.Graphics.Display.Core;
using Windows.System.Profile;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Controls;

/// <summary>
/// Represents a custom web view control for interacting with a Jellyfin server.
/// </summary>
public sealed partial class JellyfinWebView : UserControl
{
    private readonly MessageHandler _messageHandler;
    private WebView2 _wView;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinWebView"/> class.
    /// </summary>
    public JellyfinWebView()
    {
        InitializeComponent();

        _messageHandler = new MessageHandler(Window.Current.Content as Frame);

        if (Central.Settings.JellyfinServerValidated)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await InitialiseWebView().ConfigureAwait(true);
            });
        }
        else
        {
            BeginServerValidation();
        }
    }

    private void BeginServerValidation()
    {
        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
        {
            try
            {
                var jellyfinServerCheck = await ServerCheckUtil.IsJellyfinServerUrlValidAsync(new Uri(Central.Settings.JellyfinServer)).ConfigureAwait(true);
                // Check if the parsed URI is pointing to a Jellyfin server.
                if (!jellyfinServerCheck.IsValid)
                {
                    MessageDialog md = new MessageDialog($"The jellyfin server '{Central.Settings.JellyfinServer}' is currently not available: \r\n" +
                        $" {jellyfinServerCheck.ErrorMessage}");
                    await md.ShowAsync();
                    (Window.Current.Content as Frame).Navigate(typeof(OnBoarding));
                    return;
                }

                if (ServerCheckUtil.IsFutureUnsupportedVersion)
                {
                    DepricationNoticeGrid.Visibility = Visibility.Visible;
                    _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith((f) =>
                    {
                        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            ExitStoryboard.Begin();
                        });
                    });
                }

                await InitialiseWebView().ConfigureAwait(true);
            }
            finally
            {
                ProgressIndicator.Visibility = Visibility.Collapsed;
            }
        });
    }

    private async Task InitialiseWebView()
    {
        _wView = new WebView2();

        _wView.CoreWebView2Initialized += WView_CoreWebView2Initialized;
        _wView.NavigationCompleted += JellyfinWebView_NavigationCompleted;
        _wView.WebMessageReceived += OnWebMessageReceived;

        var hdmiInfo = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiInfo != null)
        {
            hdmiInfo.DisplayModesChanged += OnDisplayModeChanged;
        }

        await InitializeWebViewAndNavigateTo(new Uri(Central.Settings.JellyfinServer)).ConfigureAwait(true);
    }

    private async Task InitializeWebViewAndNavigateTo(Uri uri)
    {
        await _wView.EnsureCoreWebView2Async();
        if (_wView.CoreWebView2 == null)
        {
            await new MessageDialog("Could not initialise WebView.").ShowAsync();
            Debug.WriteLine("Failed to EnsureCoreWebView2");
            Application.Current.Exit();
        }

        AddDeviceFormToUserAgent();
        await InjectNativeShellScript().ConfigureAwait(true);

        _wView.Source = uri;
    }

    private async Task InjectNativeShellScript()
    {
        string nativeShellScript = await NativeShellScriptLoader.LoadNativeShellScript().ConfigureAwait(true);
        try
        {
            await _wView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(nativeShellScript);
            Debug.WriteLine("Injected nativeShellScript");
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to add NativeShell JS: " + ex.Message);
        }
    }

    private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var jsonMessage = args.TryGetWebMessageAsString();
            if (JsonObject.TryParse(jsonMessage, out JsonObject argsJson))
            {
                _messageHandler.HandleJsonNotification(argsJson).GetAwaiter().GetResult();
            }
            else
            {
                Debug.WriteLine($"Failed to parse args as JSON: {jsonMessage}");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to process OnWebMessageReceived: {e}");
        }
    }

    private void WView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        // Must wait for CoreWebView2 to be initialized or the WebView2 would be unfocusable.
        ContentGrid.Children.Clear();
        ContentGrid.Children.Add(_wView);
        _wView.Focus(FocusState.Programmatic);

        // Set useragent to Xbox and WebView2 since WebView2 only sets these in Sec-CA-UA, which isn't available over HTTP.
        // _wView.CoreWebView2.Settings.UserAgent += " WebView2 " + Utils.AppUtils.GetDeviceFormFactorType().ToString();

        _wView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false; // Disable autofill on Xbox as it puts down the virtual keyboard.
        _wView.CoreWebView2.ContainsFullScreenElementChanged += JellyfinWebView_ContainsFullScreenElementChanged;
    }

    private void AddDeviceFormToUserAgent()
    {
        string userAgent = _wView.CoreWebView2.Settings.UserAgent;
        string deviceForm = AnalyticsInfo.DeviceForm;

        if (!userAgent.Contains(deviceForm) && !string.Equals(deviceForm, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            const string ToReplace = ")";
            string userAgentWithDeviceForm = new Regex(Regex.Escape(ToReplace))
                .Replace(userAgent, "; " + deviceForm + ToReplace, 1);

            _wView.CoreWebView2.Settings.UserAgent = userAgentWithDeviceForm;
        }
    }

    private void JellyfinWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
        {
            if (!args.IsSuccess)
            {
                CoreWebView2WebErrorStatus errorStatus = args.WebErrorStatus;
                MessageDialog md = new MessageDialog($"Navigation failed: {errorStatus}");
                await md.ShowAsync();
            }

            ProgressIndicator.Visibility = Visibility.Collapsed;
        });
    }

    private void JellyfinWebView_ContainsFullScreenElementChanged(CoreWebView2 sender, object args)
    {
        var appView = ApplicationView.GetForCurrentView();

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

    private void OnDisplayModeChanged(HdmiDisplayInformation sender, object args)
    {
        _ = Task.Run(async () =>
        {
            string nativeShellScript = await NativeShellScriptLoader.LoadNativeShellScript();
            try
            {
                await _wView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(nativeShellScript);
                Debug.WriteLine("Injected nativeShellScript on display mode change");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to inject script on display mode change: " + ex.Message);
            }
        });
    }
}
