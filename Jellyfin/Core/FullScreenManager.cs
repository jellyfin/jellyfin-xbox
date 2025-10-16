using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Windows.Data.Json;
using Windows.Devices.Display;
using Windows.Graphics.Display;
using Windows.Graphics.Display.Core;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Jellyfin.Core;

/// <summary>
/// Responsible for changing the full screen mode to best match the video content.
/// </summary>
public sealed class FullScreenManager : IFullScreenManager
{
    private readonly ApplicationView _applicationView;
    private readonly Frame _frame;
    private readonly DisplayRequest _displayRequest;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullScreenManager"/> class.
    /// </summary>
    /// <param name="applicationView">The <see cref="ApplicationView"/> instance used to manage the application's view state.</param>
    /// <param name="frame">The root frame.</param>
    /// <param name="displayRequest">The display Request.</param>
    public FullScreenManager(ApplicationView applicationView, Frame frame, DisplayRequest displayRequest)
    {
        _applicationView = applicationView;
        _frame = frame;
        _displayRequest = displayRequest;
    }

    private async Task SwitchToBestDisplayMode(uint videoWidth, uint videoHeight, double videoFrameRate, HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        var hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();

        var bestDisplayMode =
            GetBestDisplayMode(hdmiDisplayInformation, videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption);
        if (bestDisplayMode != null && bestDisplayMode.Any())
        {
            foreach (var item in bestDisplayMode)
            {
                if (await hdmiDisplayInformation
                    ?.RequestSetCurrentDisplayModeAsync(item))
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

        var supportedDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes();
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
        return mode => refreshRate > mode.RefreshRate;
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
        var supportedHdmiDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes();

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
            .Where(MinRefreshRateMatches(videoHeight - 3))
            .OrderBy(e => e.ResolutionHeightInRawPixels * e.ResolutionWidthInRawPixels)
            .ThenBy(e => e.RefreshRate);
    }

    private async Task SetDefaultDisplayModeAsync()
    {
        await HdmiDisplayInformation.GetForCurrentView()?.SetDefaultDisplayModeAsync();
    }

    /// <summary>
    /// Enables Fullscreen.
    /// </summary>
    /// <param name="args">JsonObject.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public async Task EnableFullscreenAsync(JsonObject args)
    {
        if (AppUtils.IsXbox)
        {
            if (args != null)
            {
                try
                {
                    var videoWidth = (uint)args.GetNamedNumber("videoWidth");
                    var videoHeight = (uint)args.GetNamedNumber("videoHeight");
                    var videoFrameRate = args.GetNamedNumber("videoFrameRate");
                    var videoRangeType = args.GetNamedString("videoRangeType");

                    var hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
                    var hdmiDisplayHdrOption = GetHdmiDisplayHdrOption(hdmiDisplayInformation, videoRangeType);
                    await SwitchToBestDisplayMode(videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption).ConfigureAwait(false);
                    _displayRequest.RequestActive();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error during SwitchToBestDisplayMode", ex);
                }
            }
            else
            {
                Debug.WriteLine("enableFullscreenAsync called with no args");
            }
        }
        else
        {
            _applicationView.TryEnterFullScreenMode();
        }
    }

    /// <summary>
    /// Disables FullScreen.
    /// </summary>
    /// <returns>A task that completes when the fullscreen has been closed.</returns>
    public async Task DisableFullScreen()
    {
        if (AppUtils.IsXbox)
        {
            await SetDefaultDisplayModeAsync().ConfigureAwait(true);
        }
        else
        {
            _applicationView.ExitFullScreenMode();
        }

        _displayRequest.RequestRelease();
    }
}
