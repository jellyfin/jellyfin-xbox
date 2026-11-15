namespace Jellyfin.Core;

/// <summary>
/// Defines which engine is used to play video.
/// </summary>
public enum PlayerMode
{
    /// <summary>
    /// The HTML5 player of the web UI.
    /// </summary>
    Web = 0,

    /// <summary>
    /// The native UWP media player.
    /// </summary>
    Native = 1,

    /// <summary>
    /// The VLC player.
    /// </summary>
    Vlc = 2,
}
