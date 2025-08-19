namespace Jellyfin.Core.Contract;

/// <summary>
/// Defines Properties and methods for managing application settings.
/// </summary>
public interface ISettingsManager
{
    /// <summary>
    /// Gets a value indicating whether a Jellyfin server is configured.
    /// </summary>
    bool HasJellyfinServer { get; }

    /// <summary>
    /// Gets or sets the configured Jellyfin server address.
    /// </summary>
    string JellyfinServer { get; set; }

    /// <summary>
    /// Gets a value indicating whether the state of the <see cref="JellyfinServer"/>s validation state.
    /// </summary>
    bool JellyfinServerValidated { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the display resolution should be set to match the video resolution.
    /// </summary>
    bool AutoResolution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the display refresh rate should be set to match the video refresh rate.
    /// </summary>
    bool AutoRefreshRate { get; set; }

    /// <summary>
    /// Gets the value of a property from the settings container.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the property is not found.</param>
    /// <returns>The value of the property if found; otherwise, the default value.</returns>
    T GetProperty<T>(string propertyName, T defaultValue = default);
}
