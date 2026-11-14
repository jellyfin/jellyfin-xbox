using System;
using Windows.Data.Json;
using Windows.UI.Xaml;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Connects the web UI player plugin of winuwp.js with the native video players.
/// </summary>
public interface INativePlayerBridge
{
    /// <summary>
    /// Raised when the video surface should be shown (element) or hidden (null).
    /// </summary>
    event EventHandler<FrameworkElement> SurfaceChanged;

    /// <summary>
    /// Raised when a json message should be posted to the web UI.
    /// </summary>
    event EventHandler<string> MessageToWeb;

    /// <summary>
    /// Handles a player message of the web UI.
    /// </summary>
    /// <param name="type">The message type, starting with "player.".</param>
    /// <param name="args">The message arguments.</param>
    void HandleMessage(string type, JsonObject args);

    /// <summary>
    /// Stops playback and releases the player.
    /// </summary>
    void Reset();
}
