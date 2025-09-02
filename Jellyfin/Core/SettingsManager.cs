using Jellyfin.Core.Contract;
using Windows.Storage;

namespace Jellyfin.Core;

/// <summary>
/// Manages application settings for Jellyfin, including server configuration.
/// </summary>
public class SettingsManager : ISettingsManager
{
    private string _containerSettings = "APPSETTINGS";
    private string _settingsServer = "SERVER";
    private string _autoResolution = "AUTO_RESOLUTION";
    private string _autoRefreshRate = "AUTO_REFRESH_RATE";
    private string _forceEnableTvMode = "FORCE_TV_MODE";

    private ApplicationDataContainer LocalSettings => ApplicationData.Current.LocalSettings;

    private ApplicationDataContainer ContainerSettings
    {
        get
        {
            if (!LocalSettings.Containers.ContainsKey(_containerSettings))
            {
                LocalSettings.CreateContainer(_containerSettings, ApplicationDataCreateDisposition.Always);
            }

            return LocalSettings.Containers[_containerSettings];
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
        get => GetProperty<string>(_settingsServer);
        set => SetProperty(_settingsServer, value);
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
        get => GetProperty<bool>(_autoResolution);
        set => SetProperty(_autoResolution, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the display refresh rate should be set to match the video refresh rate.
    /// </summary>
    public bool AutoRefreshRate
    {
        get => GetProperty<bool>(_autoRefreshRate);
        set => SetProperty(_autoRefreshRate, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to force enable TV mode, which may adjust UI elements for better TV compatibility.
    /// </summary>
    public bool ForceEnableTvMode
    {
        get => GetProperty<bool>(_forceEnableTvMode);
        set => SetProperty(_forceEnableTvMode, value);
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
