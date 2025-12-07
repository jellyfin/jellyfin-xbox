using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Jellyfin.Core;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Graphics.Display;
using Windows.Graphics.Display.Core;
using Windows.Storage;
using Windows.System.Display;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using UnhandledExceptionEventArgs = Windows.UI.Xaml.UnhandledExceptionEventArgs;

namespace Jellyfin;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        if (!ApplicationViewScaling.TrySetDisableLayoutScaling(true))
        {
            throw new InvalidOperationException("Failed to disable layout scaling.");
        }

        DisplayRequest = new();

        Suspending += OnSuspending;
        RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

        Services = ConfigureServices();

        UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Gets the Display request object for handling screen activation.
    /// </summary>
    public static DisplayRequest DisplayRequest { get; private set; }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
#pragma warning disable IDISP006
    public IServiceProvider Services { get; }
#pragma warning restore IDISP006

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ViewModels
        services.AddTransient<JellyfinWebViewModel>();
        services.AddTransient<OnBoardingViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Core
        services.AddTransient<Frame>(_ => Window.Current.Content as Frame);
        services.AddTransient<CoreDispatcher>(_ => Window.Current.Dispatcher);
        services.AddTransient<ApplicationView>(_ => ApplicationView.GetForCurrentView());
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);

        // Services
        services.AddSingleton<IFullScreenManager, FullScreenManager>();
        services.AddSingleton<IMessageHandler, MessageHandler>();
        services.AddSingleton<INativeShellScriptLoader, NativeShellScriptLoader>();
        services.AddSingleton<ISettingsManager, SettingsManager>();
        services.AddSingleton<IGamepadManager, GamepadManager>();
        services.AddSingleton<DisplayRequest>(_ => App.DisplayRequest);

        services.AddLogging(e => e.AddConsole().AddProvider(new RollingAppLoggerProvider()));

#pragma warning disable IDISP005
        return services.BuildServiceProvider();
#pragma warning restore IDISP005
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Services.GetRequiredService<ILogger<App>>().LogCritical(e.Exception, "Unhandled exception occurred");
        e.Handled = true;

        _ = App.Current.Services.GetRequiredService<CoreDispatcher>().RunAsync(CoreDispatcherPriority.High, async () =>
        {
            if (Central.Settings.HasJellyfinServer && Central.Settings.JellyfinServerValidated && !string.IsNullOrWhiteSpace(Central.Settings.JellyfinServerAccessToken))
            {
                var md = new MessageDialog($"The Application has encountered an unexpected issue. Do you want to attempt to upload the logfiles to your Jellyfin server?", "Unexpected error.");
                md.Commands.Add(new UICommand("Yes", command =>
                {
                    Task.Run(async () =>
                    {
                        await UploadClientLog().ConfigureAwait(false);
                        Exit();
                    });
                }));
                md.Commands.Add(new UICommand("No", command =>
                {
                    Exit();
                }));
                await md.ShowAsync();
            }
            else
            {
                var md = new MessageDialog($"The Application has encountered an unexpected issue and will now close.", "Unexpected error.");
                md.Commands.Add(new UICommand("Ok", command => Exit()));
                await md.ShowAsync();
            }
        });
    }

    private async Task<bool> UploadClientLog()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("X-Emby-Token", Central.Settings.JellyfinServerAccessToken);
            httpClient.BaseAddress = new Uri(Central.Settings.JellyfinServer);
            var loggerProvider = (RollingAppLoggerProvider)Services.GetRequiredService<ILoggerProvider>();
            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"Jellyfin for Xbox Client version {Assembly.GetCallingAssembly().GetName().Version}");
            logBuilder.AppendLine($"UWP version: {AnalyticsInfo.VersionInfo.DeviceFamily} {AnalyticsInfo.VersionInfo.DeviceFamilyVersion}");
            logBuilder.AppendLine($"Device info: {AnalyticsInfo.DeviceForm}");

            foreach (var deviceInfo in await AnalyticsInfo.GetSystemPropertiesAsync([
                         "App",
                         "AppVer",
                         "DeviceFamily",
                         "FlightRing",
                         "OSVersionFull",
                     ]))
            {
                logBuilder.AppendLine($"{deviceInfo.Key}: {deviceInfo.Value}");
            }

            foreach (var loggerProviderLog in loggerProvider.Logs)
            {
                logBuilder.AppendLine($"[{loggerProviderLog.Timestamp:u}] [{loggerProviderLog.Level}] {loggerProviderLog.Message}");
                if (loggerProviderLog.Exception != null)
                {
                    logBuilder.AppendLine(loggerProviderLog.Exception.ToString());
                }
            }

            using var response = await httpClient.PostAsync("/ClientLog/Document", new StringContent(logBuilder.ToString())).ConfigureAwait(false);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch
        {
            // really no point in logging here, the log will never show up anywhere.
            return false;
        }
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="e">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        var rootFrame = Window.Current.Content as Frame;

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (rootFrame == null)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;

            if (AppUtils.IsXbox)
            {
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            }
            else
            {
                ApplicationViewTitleBar formattableTitleBar = ApplicationView.GetForCurrentView().TitleBar;
                formattableTitleBar.ButtonBackgroundColor = Color.FromArgb(255, 32, 32, 32);
                formattableTitleBar.ButtonForegroundColor = Color.FromArgb(255, 160, 160, 160);
                formattableTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                formattableTitleBar.BackgroundColor = Color.FromArgb(255, 32, 32, 32);
            }

            if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
            {
                // TODO: Load state from previously suspended application
            }

            // Place the frame in the current Window
            Window.Current.Content = rootFrame;
        }

        if (e.PrelaunchActivated == false)
        {
            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (Core.Central.Settings.HasJellyfinServer)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                else
                {
                    rootFrame.Navigate(typeof(Views.OnBoarding), e.Arguments);
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails.
    /// </summary>
    /// <param name="sender">The Frame which failed navigation.</param>
    /// <param name="e">Details about the navigation failure.</param>
    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
    }

    /// <summary>
    /// Invoked when application execution is being suspended.  Application state is saved
    /// without knowing whether the application will be terminated or resumed with the contents
    /// of memory still intact.
    /// </summary>
    /// <param name="sender">The source of the suspend request.</param>
    /// <param name="e">Details about the suspend request.</param>
    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        // TODO: Save application state and stop any background activity
        deferral.Complete();
    }
}
