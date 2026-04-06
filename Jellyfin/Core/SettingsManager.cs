using System;
using Jellyfin.Core.Contract;
using Windows.Storage;

namespace Jellyfin.Core;

/// <summary>
/// Manages application settings for Jellyfin, including server configuration.
/// </summary>
public class SettingsManager : ISettingsManager
{
    private const string ContainerSettingsKey = "APPSETTINGS";
    private const string SettingsServerKey = "SERVER";
    private const string SettingsServerVersionKey = "SERVER_VERSION";
    private const string AutoResolutionKey = "AUTO_RESOLUTION";
    private const string AutoRefreshRateKey = "AUTO_REFRESH_RATE";
    private const string ForceEnableTvModeKey = "FORCE_TV_MODE";

    private ApplicationDataContainer LocalSettings => ApplicationData.Current.LocalSettings;

    private ApplicationDataContainer ContainerSettings
    {
        get
        {
            if (!LocalSettings.Containers.ContainsKey(ContainerSettingsKey))
            {
                LocalSettings.CreateContainer(ContainerSettingsKey, ApplicationDataCreateDisposition.Always);
            }

            return LocalSettings.Containers[ContainerSettingsKey];
        }
    }

    /// <summary>
    /// Gets a value indicating whether a Jellyfin server is configured.
    /// </summary>
    public bool HasJellyfinServer => !string.IsNullOrEmpty(JellyfinServer);

    /// <summary>
    /// Gets or sets the configured Jellyfin server address.
    /// </summary>
    public string JellyfinServer
    {
        get => GetProperty<string>(SettingsServerKey);
        set => SetProperty(SettingsServerKey, value);
    }

    /// <summary>
    /// Gets or sets the configured Jellyfin server address.
    /// </summary>
    public Version? JellyfinServerVersion
    {
        get
        {
            var versionString = GetProperty<string>(SettingsServerVersionKey);
            if (Version.TryParse(versionString, out var version))
            {
                return version;
            }

            return null;
        }
        set => SetProperty(SettingsServerVersionKey, value.ToString());
    }

    /// <summary>
    /// Gets a value indicating whether the state of the <see cref="JellyfinServer"/>s validation state.
    /// </summary>
    public bool JellyfinServerValidated { get; internal set; }

    /// <summary>
    /// Gets a value representing the last access token used to communicating with the jellyfin server.
    /// </summary>
    public string JellyfinServerAccessToken { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the display resolution should be set to match the video resolution.
    /// </summary>
    public bool AutoResolution
    {
        get => GetProperty<bool>(AutoResolutionKey);
        set => SetProperty(AutoResolutionKey, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the display refresh rate should be set to match the video refresh rate.
    /// </summary>
    public bool AutoRefreshRate
    {
        get => GetProperty<bool>(AutoRefreshRateKey);
        set => SetProperty(AutoRefreshRateKey, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to force enable TV mode, which may adjust UI elements for better TV compatibility.
    /// </summary>
    public bool ForceEnableTvMode
    {
        get => GetProperty<bool>(ForceEnableTvModeKey);
        set => SetProperty(ForceEnableTvModeKey, value);
    }

    private void SetProperty(string propertyName, object value)
    {
        ContainerSettings.Values[propertyName] = value;
    }

    /// <summary>
    /// Gets the value of a property from the settings container.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the property is not found.</param>
    /// <returns>The value of the property if found; otherwise, the default value.</returns>
    public T GetProperty<T>(string propertyName, T defaultValue = default)
    {
        var value = ContainerSettings.Values[propertyName];

        if (value != null)
        {
            return (T)value;
        }

        return defaultValue;
    }
}
