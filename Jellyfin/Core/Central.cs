namespace Jellyfin.Core;

/// <summary>
/// Provides access to core application services and managers.
/// </summary>
public static class Central
{
    /// <summary>
    /// Gets the settings manager for application configuration.
    /// </summary>
    public static SettingsManager Settings { get; } = new SettingsManager();
}
