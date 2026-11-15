using System;
using System.Collections.Generic;

namespace Jellyfin.Core;

/// <summary>
/// Describes what a native player should play.
/// </summary>
public sealed class NativePlayRequest
{
    /// <summary>
    /// Gets or sets the stream url.
    /// </summary>
    public Uri Url { get; set; }

    /// <summary>
    /// Gets or sets the position to start playback at.
    /// </summary>
    public TimeSpan StartPosition { get; set; }

    /// <summary>
    /// Gets or sets the jellyfin stream index of the audio stream to select. -1 for the container default.
    /// </summary>
    public int AudioStreamIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the jellyfin stream index of the subtitle stream to select. -1 for none.
    /// </summary>
    public int SubtitleStreamIndex { get; set; } = -1;

    /// <summary>
    /// Gets the jellyfin stream indices of all audio streams in container order.
    /// </summary>
    public IList<int> AudioStreams { get; } = new List<int>();

    /// <summary>
    /// Gets all subtitle streams in container order.
    /// </summary>
    public IList<NativeSubtitle> Subtitles { get; } = new List<NativeSubtitle>();
}
