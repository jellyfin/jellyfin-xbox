using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Windows.Graphics.Display.Core;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.System.Profile;

namespace Jellyfin.Core;

/// <summary>
/// Injects the NativeShell javascript script to be able to interact with the UWP code.
/// </summary>
public class NativeShellScriptLoader : INativeShellScriptLoader
{
    private static readonly Uri StorageUri = new Uri("ms-appx:///Resources/winuwp.js");

    /// <summary>
    /// LoadNativeShellScript.
    /// </summary>
    /// <returns><see cref="Task"/>representing the asynchronous operation.</returns>
    public async Task<string> LoadNativeShellScript()
    {
        var storageFile = await StorageFile.GetFileFromApplicationUriAsync(StorageUri);
        var nativeShellScript = await FileIO.ReadTextAsync(storageFile);

        var assembly = Assembly.GetExecutingAssembly();
        nativeShellScript = nativeShellScript.Replace(
            "APP_NAME",
            Wrap(assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title, '\''));
        nativeShellScript = nativeShellScript.Replace("APP_VERSION", Wrap(assembly.GetName().Version.ToString(), '\''));

        nativeShellScript = nativeShellScript.Replace("DEVICE_NAME", Wrap(ResolveDeviceName(), '\''));
        nativeShellScript = nativeShellScript.Replace("FORCE_HEVC_SUPPORT", Central.Settings.ForceEnableHevcSupport.ToString().ToLower());

        var hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiDisplayInformation != null)
        {
            var supportedDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes().Where(mode => !mode.StereoEnabled);
            var supportsHdr = supportedDisplayModes.Any(mode => mode.IsSmpte2084Supported);
            nativeShellScript = nativeShellScript.Replace("SUPPORTS_HDR", supportsHdr.ToString().ToLower());
            var supportsDovi = supportedDisplayModes.Any(mode => mode.IsDolbyVisionLowLatencySupported);
            nativeShellScript = nativeShellScript.Replace("SUPPORTS_DOVI", supportsDovi.ToString().ToLower());
            var supports4K = supportedDisplayModes.Any(mode => mode.ResolutionWidthInRawPixels >= 3840 && mode.ResolutionHeightInRawPixels >= 2160);
            if (Central.Settings.ForceEnable4KSupport)
            {
                supports4K = true;
            }

            nativeShellScript = nativeShellScript.Replace("SUPPORTS_4K", supports4K.ToString().ToLower());
        }
        else
        {
            nativeShellScript = nativeShellScript.Replace("SUPPORTS_HDR", "undefined");
            nativeShellScript = nativeShellScript.Replace("SUPPORTS_DOVI", "undefined");
            nativeShellScript = nativeShellScript.Replace("SUPPORTS_4K", Central.Settings.ForceEnable4KSupport ? "true" : "undefined");
        }

        return nativeShellScript;
    }

    private static string ResolveDeviceName()
    {
        var deviceName = AnalyticsInfo.DeviceForm;
        if (string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            deviceName = AppUtils.GetDeviceFormFactorType().ToString();
        }

        if (!AppUtils.IsXbox)
        {
            return deviceName;
        }

        try
        {
            var productName = new EasClientDeviceInformation().SystemProductName;
            if (!string.IsNullOrWhiteSpace(productName) && productName.Contains("xbox", StringComparison.OrdinalIgnoreCase))
            {
                return productName;
            }
        }
        catch
        {
            return deviceName;
        }

        return DeviceFormFactorType.Xbox.ToString();
    }

    private static string Wrap(string text, char c)
    {
        return c + text + c;
    }
}
