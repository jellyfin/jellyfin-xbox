using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Jellyfin.Core;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Jellyfin.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Data.Json;
using Windows.Graphics.Display.Core;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.ViewModels;

/// <summary>
/// ViewModel for the Jellyfin WebView.
/// </summary>
public sealed class JellyfinWebViewModel : ObservableObject, IDisposable
{
    private readonly INativeShellScriptLoader _nativeShellScriptLoader;
    private readonly IMessageHandler _messageHandler;
    private readonly IGamepadManager _gamepadManager;
    private readonly IDisposable _navigationHandler;
    private readonly CoreDispatcher _dispatcher;
    private readonly Frame _frame;
    private bool _isInProgress;
    private bool _displayDeprecationNotice;
    private WebView2 _webView;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinWebViewModel"/> class.
    /// </summary>
    /// <param name="nativeShellScriptLoader">Service for loading and prepping the window injection script.</param>
    /// <param name="messageHandler">Service for handling messages send by the WinUI.</param>
    /// <param name="gamepadManager">Service for handling gamepad input.</param>
    /// <param name="dispatcher">UI dispatcher.</param>
    /// <param name="frame">Current frame of the top application.</param>
    public JellyfinWebViewModel(
        INativeShellScriptLoader nativeShellScriptLoader,
        IMessageHandler messageHandler,
        IGamepadManager gamepadManager,
        CoreDispatcher dispatcher,
        Frame frame)
    {
        _nativeShellScriptLoader = nativeShellScriptLoader;
        _messageHandler = messageHandler;
        _gamepadManager = gamepadManager;
        _dispatcher = dispatcher;
        _frame = frame;
        _navigationHandler = _gamepadManager.ObserveBackEvent(WebView_BackRequested, 0);

        if (Central.Settings.JellyfinServerValidated)
        {
            _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await InitialiseWebView().ConfigureAwait(true);
            });
        }
        else
        {
            BeginServerValidation();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the WebView is currently loading.
    /// </summary>
    public bool IsInProgress
    {
        get => _isInProgress;
        set => SetProperty(ref _isInProgress, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a deprecation notice should be displayed to the user.
    /// </summary>
    public bool DisplayDeprecationNotice
    {
        get => _displayDeprecationNotice;
        set => SetProperty(ref _displayDeprecationNotice, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="WebView2"/> instance used to render web content.
    /// </summary>
    /// <remarks>Ensure that the <see cref="WebView2"/> instance is properly initialized before use.  Setting
    /// this property will update the internal reference to the web view.</remarks>
    public WebView2 WebView
    {
        get => _webView;
        set => SetProperty(ref _webView, value);
    }

    private void BeginServerValidation()
    {
        _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            try
            {
                var jellyfinServerCheck = await ServerCheckUtil.IsJellyfinServerUrlValidAsync(new Uri(Central.Settings.JellyfinServer)).ConfigureAwait(true);
                // Check if the parsed URI is pointing to a Jellyfin server.
                if (!jellyfinServerCheck.IsValid)
                {
                    var md = new MessageDialog($"The jellyfin server '{Central.Settings.JellyfinServer}' is currently not available: \r\n" +
                                               $" {jellyfinServerCheck.ErrorMessage}");
                    await md.ShowAsync();
                    _frame.Navigate(typeof(OnBoarding));
                    return;
                }

                await InitialiseWebView().ConfigureAwait(true);
            }
            finally
            {
                IsInProgress = false;
            }
        });
    }

    private async Task InitialiseWebView()
    {
        if (ServerCheckUtil.IsFutureUnsupportedVersion)
        {
            DisplayDeprecationNotice = true;
        }

        WebView = new WebView2();

        WebView.CoreWebView2Initialized += WView_CoreWebView2Initialized;
        WebView.NavigationCompleted += JellyfinWebView_NavigationCompleted;
        WebView.WebMessageReceived += OnWebMessageReceived;

        var hdmiInfo = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiInfo != null)
        {
            hdmiInfo.DisplayModesChanged += OnDisplayModeChanged;
        }

        await InitializeWebViewAndNavigateTo(new Uri(Central.Settings.JellyfinServer)).ConfigureAwait(true);
    }

    private void WebView_BackRequested(BackRequestedEventArgs e)
    {
        if (WebView.CanGoBack && !e.Handled)
        {
            e.Handled = true;
            WebView.GoBack(); // Navigate back in the WebView2 control.
        }
    }

    private async Task InitializeWebViewAndNavigateTo(Uri uri)
    {
        await WebView.EnsureCoreWebView2Async();
        if (WebView.CoreWebView2 == null)
        {
            await new MessageDialog("Could not initialise WebView.").ShowAsync();
            Debug.WriteLine("Failed to EnsureCoreWebView2");
            Application.Current.Exit();
        }

        AddDeviceFormToUserAgent();
        await InjectNativeShellScript().ConfigureAwait(true);

        WebView.Source = uri;
    }

    private async Task InjectNativeShellScript()
    {
        var nativeShellScript = await _nativeShellScriptLoader.LoadNativeShellScript().ConfigureAwait(true);
        try
        {
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(nativeShellScript);
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
            if (JsonObject.TryParse(jsonMessage, out var argsJson))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _messageHandler.HandleJsonNotification(argsJson).ConfigureAwait(true);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error handling JSON message: {e}");
                    }
                });
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
        WebView.Focus(FocusState.Programmatic);

        WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false; // Disable autofill on Xbox as it puts down the virtual keyboard.
        WebView.CoreWebView2.ContainsFullScreenElementChanged += JellyfinWebView_ContainsFullScreenElementChanged;
    }

    private void AddDeviceFormToUserAgent()
    {
        // Set useragent to Xbox and WebView2 since WebView2 only sets these in Sec-CA-UA, which isn't available over HTTP.
        if (Central.Settings.ForceEnableTvMode && AppUtils.GetDeviceFormFactorType() != DeviceFormFactorType.Xbox)
        {
            WebView.CoreWebView2.Settings.UserAgent += " WebView2 Xbox";
        }
        else
        {
            WebView.CoreWebView2.Settings.UserAgent += " WebView2 " + AppUtils.GetDeviceFormFactorType().ToString();
        }

        var userAgent = WebView.CoreWebView2.Settings.UserAgent;
        var deviceForm = AnalyticsInfo.DeviceForm;

        if (!userAgent.Contains(deviceForm) && !string.Equals(deviceForm, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            const string ToReplace = ")";
            var userAgentWithDeviceForm = new Regex(Regex.Escape(ToReplace))
                .Replace(userAgent, "; " + deviceForm + ToReplace, 1);

            WebView.CoreWebView2.Settings.UserAgent = userAgentWithDeviceForm;
        }
    }

    private void JellyfinWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            if (!args.IsSuccess)
            {
                var errorStatus = args.WebErrorStatus;
                var md = new MessageDialog($"Navigation failed: {errorStatus}");
                await md.ShowAsync();
            }

            IsInProgress = false;
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
            var nativeShellScript = await _nativeShellScriptLoader.LoadNativeShellScript();
            try
            {
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(nativeShellScript);
                Debug.WriteLine("Injected nativeShellScript on display mode change");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to inject script on display mode change: " + ex.Message);
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationHandler.Dispose();
    }
}
