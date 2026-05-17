using System;
using Windows.System.Profile;

namespace Jellyfin.Utils;

/// <summary>
/// Provides utility methods for application device form factor detection.
/// </summary>
public class AppUtils
{
    private static bool? _isXbox = null;

    /// <summary>
    /// Gets a value indicating whether the current device is an Xbox or Holographic device.
    /// </summary>
    public static bool IsXbox
    {
        get
        {
            if (!_isXbox.HasValue)
            {
                var deviceType = GetDeviceFormFactorType();
                _isXbox = deviceType == DeviceFormFactorType.Xbox || deviceType == DeviceFormFactorType.Holographic;
            }

            return _isXbox.Value;
        }
    }

    /// <summary>
    /// Gets the current applications version.
    /// </summary>
    public static Version AppVersion
    {
        get
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }
    }

    /// <summary>
    /// Determines the device form factor type based on the device family.
    /// </summary>
    /// <returns>The <see cref="DeviceFormFactorType"/> of the current device.</returns>
    public static DeviceFormFactorType GetDeviceFormFactorType()
    {
        switch (AnalyticsInfo.VersionInfo.DeviceFamily)
        {
            case "Windows.Mobile":
                return DeviceFormFactorType.Mobile;
            case "Windows.Xbox":
                return DeviceFormFactorType.Xbox;
            case "Windows.Holographic":
                return DeviceFormFactorType.Holographic;
            default:
                return DeviceFormFactorType.Desktop;
        }
    }
}
