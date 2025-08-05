using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Utils;
using Windows.Data.Json;
using Windows.Graphics.Display.Core;
using Windows.UI.ViewManagement;

namespace Jellyfin.Core;

/// <summary>
/// Responsible for changing the full screen mode to best match the video content.
/// </summary>
public sealed class FullScreenManager
{
    private async Task SwitchToBestDisplayMode(uint videoWidth, uint videoHeight, double videoFrameRate, HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        HdmiDisplayMode bestDisplayMode =
            GetBestDisplayMode(videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption);
        if (bestDisplayMode != null)
        {
            await HdmiDisplayInformation.GetForCurrentView()
                ?.RequestSetCurrentDisplayModeAsync(bestDisplayMode, hdmiDisplayHdrOption);
        }
    }

    private HdmiDisplayHdrOption GetHdmiDisplayHdrOption(string videoRangeType)
    {
        HdmiDisplayInformation hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiDisplayInformation == null)
        {
            return HdmiDisplayHdrOption.None;
        }

        IReadOnlyList<HdmiDisplayMode> supportedDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes();
        bool displaySupportsDoVi = supportedDisplayModes.Any(mode => mode.IsDolbyVisionLowLatencySupported);
        bool displaySupportsHdr = supportedDisplayModes.Any(mode => mode.IsSmpte2084Supported);

        HdmiDisplayHdrOption hdrOtherwiseSdr =
            displaySupportsHdr ? HdmiDisplayHdrOption.Eotf2084 : HdmiDisplayHdrOption.None;
        HdmiDisplayHdrOption doViOtherwiseHdrOtherwiseSdr =
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

    private static Func<HdmiDisplayMode, bool> ResolutionMatches(uint width, uint height)
    {
        return mode => mode.ResolutionWidthInRawPixels == width || mode.ResolutionHeightInRawPixels == height;
    }

    private static Func<HdmiDisplayMode, bool> HdmiDisplayHdrOptionMatches(HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        bool HdrMatches(HdmiDisplayMode mode) =>
            hdmiDisplayHdrOption == HdmiDisplayHdrOption.None ||
            (hdmiDisplayHdrOption == HdmiDisplayHdrOption.DolbyVisionLowLatency && mode.IsDolbyVisionLowLatencySupported) ||
            (hdmiDisplayHdrOption == HdmiDisplayHdrOption.Eotf2084 && mode.IsSmpte2084Supported);

        return HdrMatches;
    }

    private HdmiDisplayMode GetBestDisplayMode(uint videoWidth, uint videoHeight, double videoFrameRate, HdmiDisplayHdrOption hdmiDisplayHdrOption)
    {
        HdmiDisplayInformation hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        IEnumerable<HdmiDisplayMode> supportedHdmiDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes();

        // `GetHdmiDisplayHdrOption(...)` ensures the HdmiDisplayHdrOption is always a mode the display supports
        IEnumerable<HdmiDisplayMode> hdmiDisplayModes = supportedHdmiDisplayModes.Where(HdmiDisplayHdrOptionMatches(hdmiDisplayHdrOption));

        bool filteredToVideoResolution = false;
        if (Central.Settings.AutoResolution)
        {
            IEnumerable<HdmiDisplayMode> matchingResolution = hdmiDisplayModes.Where(ResolutionMatches(videoWidth, videoHeight));
            if (matchingResolution.Any())
            {
                hdmiDisplayModes = matchingResolution;
                filteredToVideoResolution = true;
            }
        }

        HdmiDisplayMode currentHdmiDisplayMode = hdmiDisplayInformation.GetCurrentDisplayMode();

        if (!filteredToVideoResolution)
        {
            hdmiDisplayModes = hdmiDisplayModes.Where(ResolutionMatches(
                currentHdmiDisplayMode.ResolutionWidthInRawPixels,
                currentHdmiDisplayMode.ResolutionHeightInRawPixels));
        }

        if (Central.Settings.AutoRefreshRate)
        {
            IEnumerable<HdmiDisplayMode> matchingRefreshRates = hdmiDisplayModes.Where(RefreshRateMatches(videoFrameRate));
            if (matchingRefreshRates.Any())
            {
                return matchingRefreshRates.First();
            }
        }

        return hdmiDisplayModes
            .Where(ResolutionMatches(
                currentHdmiDisplayMode.ResolutionWidthInRawPixels,
                currentHdmiDisplayMode.ResolutionHeightInRawPixels))
            .Where(RefreshRateMatches(currentHdmiDisplayMode.RefreshRate))
            .FirstOrDefault();
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
                    uint videoWidth = (uint)args.GetNamedNumber("videoWidth");
                    uint videoHeight = (uint)args.GetNamedNumber("videoHeight");
                    double videoFrameRate = args.GetNamedNumber("videoFrameRate");
                    string videoRangeType = args.GetNamedString("videoRangeType");
                    HdmiDisplayHdrOption hdmiDisplayHdrOption = GetHdmiDisplayHdrOption(videoRangeType);
                    await SwitchToBestDisplayMode(videoWidth, videoHeight, videoFrameRate, hdmiDisplayHdrOption);
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
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }
    }

    /// <summary>
    /// Disables FullScreen.
    /// </summary>
    public async void DisableFullScreen()
    {
        if (AppUtils.IsXbox)
        {
            await SetDefaultDisplayModeAsync();
        }
        else
        {
            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }
    }
}
