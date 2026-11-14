using System;

namespace Jellyfin.Core;

/// <summary>
/// Event data raised by a native player.
/// </summary>
public sealed class NativePlayerEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativePlayerEventArgs"/> class.
    /// </summary>
    /// <param name="name">The event name (opened, timeupdate, pause, unpause, volumechange, ended, error).</param>
    public NativePlayerEventArgs(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the current position in milliseconds.
    /// </summary>
    public double PositionMs { get; set; }

    /// <summary>
    /// Gets or sets the duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the player is paused.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Gets or sets the volume in the range of 0 to 100.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the player is muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets an optional message, e.g. an error description.
    /// </summary>
    public string Message { get; set; }
}
