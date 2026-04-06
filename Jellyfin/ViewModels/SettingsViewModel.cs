using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jellyfin.Core;
using Jellyfin.Core.Contract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Display.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace Jellyfin.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IGamepadManager _gamepadManager;
    private readonly CoreDispatcher _coreDispatcher;
    private readonly IStringLocalizer<Translations> _stringLocalizer;
    private readonly IDisposable _navigationHandler;
    private HdmiDisplayInformation _currentHdmiDisplayInformation;
    private bool _autoRefreshRate;
    private bool _autoResolution;
    private bool _forceEnableTvMode;
    private HdmiDisplayMode _currentDisplayMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="gamepadManager">The <see cref="IGamepadManager"/> instance used to handle gamepad-related events.</param>
    /// <param name="coreDispatcher">The Dispatcher.</param>
    /// <param name="stringLocalizer">The localizer service.</param>
    public SettingsViewModel(IGamepadManager gamepadManager, CoreDispatcher coreDispatcher, IStringLocalizer<Translations> stringLocalizer)
    {
        _gamepadManager = gamepadManager;
        _coreDispatcher = coreDispatcher;
        _stringLocalizer = stringLocalizer;
        AutoRefreshRate = Central.Settings.AutoRefreshRate;
        AutoResolution = Central.Settings.AutoResolution;
        ForceEnableTvMode = Central.Settings.ForceEnableTvMode;

        _navigationHandler = _gamepadManager.ObserveBackEvent(ModalPage_BackRequested, -10);
        try
        {
            CurrentHdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
            CurrentDisplayMode = CurrentHdmiDisplayInformation.GetCurrentDisplayMode();
            PossibleDisplayModes = new(CurrentHdmiDisplayInformation.GetSupportedDisplayModes());
        }
        catch (Exception e)
        {
            Debug.Write(e);
        }

        Logs = new();

        Task.Run(async () =>
        {
            try
            {
                if (App.Current is App currentApp)
                {
                    var loggerProvider = (FileBackedLoggerProvider)currentApp.Services.GetRequiredService<ILoggerProvider>();
                    var logfiles = await loggerProvider.GetLogfiles().ConfigureAwait(false);
                    _ = _coreDispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        var orderedLogs = logfiles.OrderByDescending(e => e.DateCreated).ToArray();
                        foreach (var log in orderedLogs)
                        {
                            Logs.Add(new LogfileViewModel(log)
                            {
                                UploadCallback = UploadLogfileCommand.Execute,
                                IsLatestLogfile = orderedLogs[0] == log
                            });
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Debug.Write(e);
            }
        });

        SaveCommand = new RelayCommand(OnSaveExecute);
        AbortCommand = new RelayCommand(OnAbortExecute);
        UploadLogfileCommand = new AsyncRelayCommand<LogfileViewModel>(OnUploadLogfileExecute);
    }

    /// <summary>
    /// Gets the version number of the currently executing application assembly.
    /// </summary>
    public string AppVersion { get => Assembly.GetCallingAssembly().GetName().Version.ToString(); }

    /// <summary>
    /// Gets or sets a value indicating whether to force enable TV mode.
    /// </summary>
    public bool ForceEnableTvMode
    {
        get => _forceEnableTvMode;
        set => SetProperty(ref _forceEnableTvMode, value);
    }

    /// <summary>
    /// Gets or sets the collection of possible HDMI display modes.
    /// </summary>
    public ObservableCollection<HdmiDisplayMode> PossibleDisplayModes { get; set; }

    /// <summary>
    /// Gets or sets the collection of log file view models displayed in the user interface.
    /// </summary>
    public ObservableCollection<LogfileViewModel> Logs { get; set; }

    /// <summary>
    /// Gets or sets the current HDMI display mode.
    /// </summary>
    public HdmiDisplayMode CurrentDisplayMode
    {
        get => _currentDisplayMode;
        set => SetProperty(ref _currentDisplayMode, value);
    }

    /// <summary>
    /// Gets or sets the HDMI display information.
    /// </summary>
    public HdmiDisplayInformation CurrentHdmiDisplayInformation
    {
        get => _currentHdmiDisplayInformation;
        set => SetProperty(ref _currentHdmiDisplayInformation, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the auto refresh rate setting is enabled.
    /// </summary>
    public bool AutoRefreshRate
    {
        get => _autoRefreshRate;
        set => SetProperty(ref _autoRefreshRate, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the auto resolution setting is enabled.
    /// </summary>
    public bool AutoResolution
    {
        get => _autoResolution;
        set => SetProperty(ref _autoResolution, value);
    }

    /// <summary>
    /// Gets or sets the command to upload the logfile.
    /// </summary>
    public IRelayCommand UploadLogfileCommand { get; set; }

    /// <summary>
    /// Gets or sets the command to save the settings and return back to the web app.
    /// </summary>
    public IRelayCommand SaveCommand { get; set; }

    /// <summary>
    /// Gets or sets the command to abort the settings changes and return back to the web app.
    /// </summary>
    public IRelayCommand AbortCommand { get; set; }

    /// <summary>
    /// Gets or sets a delegate that should be invoked to close the current popup or modal page.
    /// </summary>
    public Action CloseAction { get; set; }

    private async Task OnUploadLogfileExecute(LogfileViewModel logfileViewModel)
    {
        if (App.Current is App currentApp)
        {
            var result = await currentApp.UploadClientLog(logfileViewModel?.Filename).ConfigureAwait(false);
            _ = _coreDispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                if (result)
                {
                    await new MessageDialog(_stringLocalizer.GetString("Settings.UploadLogfile.Success.Text")).ShowAsync();
                }
                else
                {
                    await new MessageDialog(_stringLocalizer.GetString("Settings.UploadLogfile.Failure.Text")).ShowAsync();
                }
            });
        }
    }

    private void OnAbortExecute()
    {
        NavigateToMainPage();
    }

    private void OnSaveExecute()
    {
        SaveSettings();
        NavigateToMainPage();
    }

    private void ModalPage_BackRequested(BackRequestedEventArgs e)
    {
        e.Handled = true;
        NavigateToMainPage();
    }

    private void NavigateToMainPage()
    {
        CloseAction();
        _navigationHandler.Dispose();
    }

    private void SaveSettings()
    {
        if (CurrentHdmiDisplayInformation != null)
        {
            Central.Settings.AutoRefreshRate = AutoRefreshRate;
            Central.Settings.AutoResolution = AutoResolution;
        }

        Central.Settings.ForceEnableTvMode = ForceEnableTvMode;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationHandler?.Dispose();
    }
}
