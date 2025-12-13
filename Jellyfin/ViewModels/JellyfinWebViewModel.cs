using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Jellyfin.Core;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Jellyfin.Views;
using Microsoft.Extensions.Logging;
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
public sealed class JellyfinWebViewModel : ObservableRecipient, IDisposable, IRecipient<WebMessage>
{
    private readonly INativeShellScriptLoader _nativeShellScriptLoader;
    private readonly IMessageHandler _messageHandler;
    private readonly IGamepadManager _gamepadManager;
    private readonly IDisposable _navigationHandler;
    private readonly CoreDispatcher _dispatcher;
    private readonly Frame _frame;
    private readonly ApplicationView _applicationView;
    private readonly ILogger<JellyfinWebViewModel> _logger;
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
    /// <param name="applicationView">Application view for managing the app's view state.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="messenger">The Messenger service.</param>
    public JellyfinWebViewModel(
        INativeShellScriptLoader nativeShellScriptLoader,
        IMessageHandler messageHandler,
        IGamepadManager gamepadManager,
        CoreDispatcher dispatcher,
        Frame frame,
        ApplicationView applicationView,
        ILogger<JellyfinWebViewModel> logger,
        IMessenger messenger) : base(messenger)
    {
        _nativeShellScriptLoader = nativeShellScriptLoader;
        _messageHandler = messageHandler;
        _gamepadManager = gamepadManager;
        _dispatcher = dispatcher;
        _frame = frame;
        _applicationView = applicationView;
        _logger = logger;
        _logger.LogInformation("JellyfinWebViewModel Initialising.");
        _navigationHandler = _gamepadManager.ObserveBackEvent(WebView_BackRequested, 0);

        Central.Settings.JellyfinServerAccessToken = null;
        IsInProgress = true;
        Messenger.Register(this);

        if (Central.Settings.JellyfinServerValidated)
        {
            _logger.LogInformation("Server is validated proceed to initialise webview.");
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(true); // this delay is nessesary to have the UI rendered at least before allowing to focus it
                _ = _dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    await Task.Yield();
                    await InitialiseWebView().ConfigureAwait(true);
                });
            });
        }
        else
        {
            _logger.LogInformation("Server is not validated yet.");
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
            var jellyfinServerCheck = await ServerCheckUtil.IsJellyfinServerUrlValidAsync(new Uri(Central.Settings.JellyfinServer)).ConfigureAwait(true);
            // Check if the parsed URI is pointing to a Jellyfin server.
            if (!jellyfinServerCheck.IsValid)
            {
                _logger.LogInformation("Server cannot be validated because: {ValidationError}.", jellyfinServerCheck.ErrorMessage);
                var md = new MessageDialog($"The jellyfin server '{Central.Settings.JellyfinServer}' is currently not available: \r\n" +
                                           $" {jellyfinServerCheck.ErrorMessage}");
                await md.ShowAsync();
                _frame.Navigate(typeof(OnBoarding));
                return;
            }

            Central.Settings.JellyfinServerValidated = true;
            _logger.LogInformation("Server is validated proceed to initialise webview.");
            await InitialiseWebView().ConfigureAwait(true);
        });
    }

    private async Task InitialiseWebView()
    {
        if (ServerCheckUtil.IsFutureUnsupportedVersion)
        {
            _logger.LogWarning("Server is deprecated.");
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
        try
        {
            if (WebView.CanGoBack && !e.Handled)
            {
                e.Handled = true;
                WebView.GoBack(); // Navigate back in the WebView2 control.
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to navigate back.");
        }
    }

    private async Task InitializeWebViewAndNavigateTo(Uri uri)
    {
        async Task ValidateWebView(Exception ex = null)
        {
            if (WebView.CoreWebView2 == null || ex != null)
            {
                _logger.LogError(ex, "WebView2 initialization failed.");
                await new MessageDialog("Could not initialise WebView.").ShowAsync();
                Application.Current.Exit();
            }
        }

        try
        {
            await WebView.EnsureCoreWebView2Async();
        }
        catch (Exception e)
        {
            await ValidateWebView(e);
            return;
        }

        await ValidateWebView();

        if (Central.ServerVersion != Central.Settings.JellyfinServerVersion)
        {
            Central.Settings.JellyfinServerVersion = Central.ServerVersion;
            _logger.LogInformation("Server version updated to {ServerVersion}", Central.ServerVersion);
            await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync();
        }

        AddDeviceFormToUserAgent();
        await InjectNativeShellScript().ConfigureAwait(true);

        WebView.Source = uri;

        _ = Task.Delay(TimeSpan.FromSeconds(8)).ContinueWith((c) =>
        {
            _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IsInProgress = false;
            });
        });
    }

    private async Task InjectNativeShellScript()
    {
        var nativeShellScript = await _nativeShellScriptLoader.LoadNativeShellScript().ConfigureAwait(true);
        try
        {
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(nativeShellScript);
            _logger.LogInformation("Native shell script injected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject native shell script.");
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
                        _logger.LogError(e, "Failed to handle json message.");
                    }
                });
            }
            else
            {
                _logger.LogError("Failed to parse json message. {JsonMessage}", jsonMessage);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle json message.");
        }
    }

    private void WView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        // Must wait for CoreWebView2 to be initialized or the WebView2 would be unfocusable.
        WebView.Focus(FocusState.Programmatic);

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; // Disable right click context menu.
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false; // Disable dev tools
        WebView.CoreWebView2.Settings.IsStatusBarEnabled = false; // Disable status bar.
        WebView.CoreWebView2.Settings.IsZoomControlEnabled = false; // Disable zoom control.
        WebView.CoreWebView2.Settings.IsScriptEnabled = true; // Enable JavaScript.
        WebView.CoreWebView2.Settings.IsIndexedDBEnabled = true; // Enable IndexedDB.        
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
        _logger.LogInformation("Navigation to {Url} is {Completed}", sender.Source, args.IsSuccess ? "Success" : "Failed");
        _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            if (!args.IsSuccess)
            {
                _logger.LogError("Failed to process navigation {Status}.", args.WebErrorStatus);
                var errorStatus = args.WebErrorStatus;
                var md = new MessageDialog($"Navigation failed: {errorStatus}");
                await md.ShowAsync();
            }
            else if (string.IsNullOrWhiteSpace(Central.Settings.JellyfinServerAccessToken))
            {
                var accessToken = await WebView.CoreWebView2.ExecuteScriptWithResultAsync("""
                                                                  JSON.parse(localStorage.getItem("jellyfin_credentials")).Servers[0].AccessToken
                                                                  """);
                if (!accessToken.Succeeded)
                {
                    _logger.LogError("Could not obtain access token for {Path}", args.NavigationId);
                }
                else
                {
                    var accessTokenText = accessToken.ResultAsJson.Trim('"'); // Remove quotes around the token.
                    if (Central.Settings.JellyfinServerAccessToken != accessTokenText && !string.IsNullOrWhiteSpace(accessTokenText))
                    {
                        Central.Settings.JellyfinServerAccessToken = accessTokenText;
                        _logger.LogInformation("Access token updated.");
                    }
                }
            }
        });
    }

    private void JellyfinWebView_ContainsFullScreenElementChanged(CoreWebView2 sender, object args)
    {
        try
        {
            if (sender.ContainsFullScreenElement)
            {
                _applicationView.TryEnterFullScreenMode();
                return;
            }

            if (_applicationView.IsFullScreenMode)
            {
                _applicationView.ExitFullScreenMode();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process Fullscreen change");
        }
    }

    private void OnDisplayModeChanged(HdmiDisplayInformation sender, object args)
    {
        _ = Task.Run(async () =>
        {
            await InjectNativeShellScript().ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationHandler.Dispose();
    }

    /// <inheritdoc />
    public void Receive(WebMessage message)
    {
        switch (message.Type)
        {
            case "loaded" when IsInProgress:
                _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    IsInProgress = false;
                });
                break;
        }
    }
}
