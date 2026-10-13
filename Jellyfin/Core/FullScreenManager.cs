using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Microsoft.Extensions.Logging;
using Windows.Data.Json;
using Windows.Graphics.Display.Core;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Core;

/// <summary>
/// Responsible for changing the full screen mode to best match the video content and keeping display active.
/// </summary>
public sealed class FullScreenManager : IFullScreenManager
{
    private readonly ApplicationView _applicationView;
    private readonly Frame _frame;
    private readonly DisplayRequest _displayRequest;
    private readonly ILogger<FullScreenManager> _logger;

    private readonly object _stateLock = new();
    private bool _displayRequestActive;
    private long _currentSessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullScreenManager"/> class.
    /// </summary>
    /// <param name="applicationView">The <see cref="ApplicationView"/> instance used to manage the application's view state.</param>
    /// <param name="frame">The root frame.</param>
    /// <param name="displayRequest">The display Request.</param>
    /// <param name="logger">Logger instance.</param>
    public FullScreenManager(ApplicationView applicationView, Frame frame, DisplayRequest displayRequest, ILogger<FullScreenManager> logger)
    {
        _applicationView = applicationView ?? throw new ArgumentNullException(nameof(applicationView));
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        _displayRequest = displayRequest ?? throw new ArgumentNullException(nameof(displayRequest));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task SwitchToBestDisplayMode(uint videoWidth, uint videoHeight, double videoFrameRate, HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        var hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiDisplayInformation == null)
        {
            return;
        }

        var bestDisplayMode =
            GetBestDisplayMode(hdmiDisplayInformation, videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption);
        if (bestDisplayMode != null && bestDisplayMode.Any())
        {
            foreach (var item in bestDisplayMode)
            {
                if (await hdmiDisplayInformation.RequestSetCurrentDisplayModeAsync(item))
                {
                    return;
                }
            }

            await SetDefaultDisplayModeAsync().ConfigureAwait(true);
        }
    }

    private HdmiDisplayHdrOption GetHdmiDisplayHdrOption(HdmiDisplayInformation hdmiDisplayInformation, string videoRangeType)
    {
        if (hdmiDisplayInformation == null)
        {
            return HdmiDisplayHdrOption.None;
        }

        var supportedDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes().Where(e => !e.StereoEnabled);
        var displaySupportsDoVi = supportedDisplayModes.Any(mode => mode.IsDolbyVisionLowLatencySupported);
        var displaySupportsHdr = supportedDisplayModes.Any(mode => mode.IsSmpte2084Supported);

        var hdrOtherwiseSdr =
            displaySupportsHdr ? HdmiDisplayHdrOption.Eotf2084 : HdmiDisplayHdrOption.None;
        var doViOtherwiseHdrOtherwiseSdr =
            displaySupportsDoVi ? HdmiDisplayHdrOption.DolbyVisionLowLatency : hdrOtherwiseSdr;

        switch (videoRangeType)
        {
            // Xbox only supports DOVI profile 5
            case "DOVI":
                return doViOtherwiseHdrOtherwiseSdr;
            case "DOVIWithHDR10":
            case "DOVIWithHLG":
            case "HDR":
            case "HDR10":
            case "HDR10Plus":
            case "HLG":
                return hdrOtherwiseSdr;
            case "DOVIWithSDR":
            case "SDR":
            case "Unknown":
            default:
                return HdmiDisplayHdrOption.None;
        }
    }

    private static Func<HdmiDisplayMode, bool> RefreshRateMatches(double refreshRate)
    {
        return mode => Math.Abs(refreshRate - mode.RefreshRate) <= 0.5;
    }

    private static Func<HdmiDisplayMode, bool> MinRefreshRateMatches(double refreshRate)
    {
        return mode => mode.RefreshRate >= refreshRate;
    }

    private static Func<HdmiDisplayMode, bool> ResolutionMatches(uint width, uint height)
    {
        return mode => mode.ResolutionWidthInRawPixels == width || mode.ResolutionHeightInRawPixels == height;
    }

    private static Func<HdmiDisplayMode, bool> MinResolutionMatches(uint width, uint height)
    {
        return mode => mode.ResolutionWidthInRawPixels >= width || mode.ResolutionHeightInRawPixels >= height;
    }

    private static Func<HdmiDisplayMode, bool> HdmiDisplayHdrOptionMatches(HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        return mode =>
            hdmiDisplayHdrOption == HdmiDisplayHdrOption.None ||
            (hdmiDisplayHdrOption == HdmiDisplayHdrOption.DolbyVisionLowLatency && mode.IsDolbyVisionLowLatencySupported) ||
            (hdmiDisplayHdrOption == HdmiDisplayHdrOption.Eotf2084 && mode.IsSmpte2084Supported);
    }

    private IEnumerable<HdmiDisplayMode> GetBestDisplayMode(HdmiDisplayInformation hdmiDisplayInformation, uint videoWidth, uint videoHeight, double videoFrameRate, HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        var supportedHdmiDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes().Where(e => !e.StereoEnabled);

        // `GetHdmiDisplayHdrOption(...)` ensures the HdmiDisplayHdrOption is always a mode the display supports
        var hdmiDisplayModes = supportedHdmiDisplayModes.Where(HdmiDisplayHdrOptionMatches(hdmiDisplayHdrOption)).ToArray();

        if (Central.Settings.AutoResolution)
        {
            var matchingResolution = hdmiDisplayModes.Where(ResolutionMatches(videoWidth, videoHeight)).ToArray();
            if (matchingResolution.Any())
            {
                hdmiDisplayModes = matchingResolution;
            }
        }

        if (Central.Settings.AutoRefreshRate)
        {
            var matchingRefreshRates = hdmiDisplayModes.Where(RefreshRateMatches(videoFrameRate)).ToArray();
            if (matchingRefreshRates.Any())
            {
                return matchingRefreshRates;
            }
        }

        return hdmiDisplayModes
            .Where(MinResolutionMatches(videoWidth, videoHeight))
            .Where(MinRefreshRateMatches(videoFrameRate))
            .OrderBy(e => e.ResolutionHeightInRawPixels * e.ResolutionWidthInRawPixels)
            .ThenBy(e => e.RefreshRate);
    }

    private async Task SetDefaultDisplayModeAsync()
    {
        try
        {
            var hdmiInfo = HdmiDisplayInformation.GetForCurrentView();
            if (hdmiInfo != null)
            {
                await hdmiInfo.SetDefaultDisplayModeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore default HDMI display mode");
        }
    }

    private async Task EnsureOnDispatcherAsync(DispatchedHandler action)
    {
        if (_frame.Dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            await _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action);
        }
    }

    private async Task RequestDisplayActiveAsync(long sessionId)
    {
        await EnsureOnDispatcherAsync(() =>
        {
            lock (_stateLock)
            {
                if (Interlocked.Read(ref _currentSessionId) != sessionId)
                {
                    _logger.LogInformation("RequestDisplayActive superseded by subsequent session; skipping activation");
                    return;
                }

                if (_displayRequestActive)
                {
                    return;
                }

                try
                {
                    _displayRequest.RequestActive();
                    _displayRequestActive = true;
                    _logger.LogDebug("DisplayRequest.RequestActive succeeded");
                }
                catch (Exception ex)
                {
                    _displayRequestActive = false;
                    _logger.LogError(ex, "DisplayRequest.RequestActive failed");
                }
            }
        });
    }

    private async Task RequestDisplayReleaseAsync()
    {
        await EnsureOnDispatcherAsync(() =>
        {
            lock (_stateLock)
            {
                if (!_displayRequestActive)
                {
                    return;
                }

                try
                {
                    _displayRequest.RequestRelease();
                    _logger.LogDebug("DisplayRequest.RequestRelease succeeded");
                }
                catch (Exception ex)
                {
                    // If RequestRelease fails (e.g. 0x8000000E), the OS does not have an active request.
                    _logger.LogWarning(ex, "DisplayRequest.RequestRelease failed");
                }
                finally
                {
                    // Always clear the flag so future playback can re-acquire DisplayRequest cleanly.
                    _displayRequestActive = false;
                }
            }
        });
    }

    /// <summary>
    /// Enables Fullscreen and keeps display awake.
    /// </summary>
    /// <param name="args">JsonObject containing video metadata, or null for default fullscreen.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public async Task EnableFullscreenAsync(JsonObject args)
    {
        long sessionId = Interlocked.Increment(ref _currentSessionId);

        if (AppUtils.IsXbox)
        {
            if (args != null && args.ContainsKey("videoWidth"))
            {
                try
                {
                    var videoWidth = (uint)args.GetNamedNumber("videoWidth");
                    var videoHeight = args.ContainsKey("videoHeight") ? (uint)args.GetNamedNumber("videoHeight") : 0;
                    var videoFrameRate = args.ContainsKey("videoFrameRate") ? args.GetNamedNumber("videoFrameRate") : 0.0;
                    var videoRangeType = args.ContainsKey("videoRangeType") ? args.GetNamedString("videoRangeType") : "SDR";

                    var hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
                    var hdmiDisplayHdrOption = GetHdmiDisplayHdrOption(hdmiDisplayInformation, videoRangeType);

                    await SwitchToBestDisplayMode(videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during SwitchToBestDisplayMode");
                }
            }
            else
            {
                _logger.LogInformation("EnableFullscreenAsync called without video parameters; skipping HDMI mode adjustment");
            }

            // Check if DisableFullScreen was invoked while SwitchToBestDisplayMode was in-flight
            if (Interlocked.Read(ref _currentSessionId) != sessionId)
            {
                _logger.LogInformation("EnableFullscreenAsync superseded by subsequent session; restoring default display mode");
                await SetDefaultDisplayModeAsync().ConfigureAwait(true);
                return;
            }

            await RequestDisplayActiveAsync(sessionId).ConfigureAwait(true);
        }
        else
        {
            await EnsureOnDispatcherAsync(() =>
            {
                _applicationView.TryEnterFullScreenMode();
            });

            // Check if DisableFullScreen was invoked while TryEnterFullScreenMode was in-flight
            if (Interlocked.Read(ref _currentSessionId) != sessionId)
            {
                _logger.LogInformation("EnableFullscreenAsync superseded by subsequent session; exiting fullscreen mode");
                await EnsureOnDispatcherAsync(() =>
                {
                    _applicationView.ExitFullScreenMode();
                });
                return;
            }

            await RequestDisplayActiveAsync(sessionId).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Disables FullScreen and releases the display request.
    /// </summary>
    /// <returns>A task that completes when the fullscreen has been closed.</returns>
    public async Task DisableFullScreen()
    {
        // Invalidate any in-flight EnableFullscreenAsync session
        Interlocked.Increment(ref _currentSessionId);

        if (AppUtils.IsXbox)
        {
            await SetDefaultDisplayModeAsync().ConfigureAwait(true);
        }
        else
        {
            await EnsureOnDispatcherAsync(() =>
            {
                _applicationView.ExitFullScreenMode();
            });
        }

        await RequestDisplayReleaseAsync().ConfigureAwait(true);
    }
}
