using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Jellyfin.Utils;
using Windows.Data.Json;
using Windows.Graphics.Display.Core;
using Windows.Media.Core;
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
        nativeShellScript = nativeShellScript.Replace("PLAYER_MODE", Central.Settings.PlayerMode.ToString());
        nativeShellScript = nativeShellScript.Replace("NATIVE_CODEC_SUPPORT", await ProbeCodecSupportAsync());

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

    private static async Task<string> GetCodecSupportAsync()
    {
        // The system codec list does not change while the app runs, so probe only once.
        return await ProbeCodecSupportAsync();
    }

    private static async Task<string> ProbeCodecSupportAsync()
    {
        var video = new JsonObject();
        var audio = new JsonObject();
        var videoCodecs = new (string Name, string Subtype)[]
        {
            ("h264", CodecSubtypes.VideoFormatH264),
            ("hevc", CodecSubtypes.VideoFormatHevc),
            ("mpeg2video", CodecSubtypes.VideoFormatMpeg2),
            ("vc1", CodecSubtypes.VideoFormatWvc1),
            ("vp8", CodecSubtypes.VideoFormatVP80),
            ("vp9", CodecSubtypes.VideoFormatVP90),
        };
        var audioCodecs = new (string Name, string Subtype)[]
        {
            ("aac", CodecSubtypes.AudioFormatAac),
            ("ac3", CodecSubtypes.AudioFormatDolbyAC3),
            ("eac3", CodecSubtypes.AudioFormatDolbyDDPlus),
            ("mp3", CodecSubtypes.AudioFormatMP3),
            ("flac", CodecSubtypes.AudioFormatFlac),
            ("opus", CodecSubtypes.AudioFormatOpus),
            ("dts", CodecSubtypes.AudioFormatDts),
        };

        try
        {
            var query = new CodecQuery();
            foreach (var (name, subtype) in videoCodecs)
            {
                var found = await query.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, subtype);
                video[name] = JsonValue.CreateBooleanValue(found.Count > 0);
            }

            foreach (var (name, subtype) in audioCodecs)
            {
                var found = await query.FindAllAsync(CodecKind.Audio, CodecCategory.Decoder, subtype);
                audio[name] = JsonValue.CreateBooleanValue(found.Count > 0);
            }
        }
        catch
        {
            video.Clear();
            audio.Clear();
        }

        var result = new JsonObject();
        result["video"] = video;
        result["audio"] = audio;
        return result.Stringify();
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
