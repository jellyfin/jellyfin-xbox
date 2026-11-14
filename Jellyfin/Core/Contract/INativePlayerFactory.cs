namespace Jellyfin.Core.Contract;

/// <summary>
/// Creates native video players.
/// </summary>
public interface INativePlayerFactory
{
    /// <summary>
    /// Creates a player for the given mode. Must be called from the UI thread.
    /// </summary>
    /// <param name="mode">The player mode. <see cref="PlayerMode.Web"/> is not supported.</param>
    /// <returns>The player.</returns>
    INativeVideoPlayer Create(PlayerMode mode);
}
