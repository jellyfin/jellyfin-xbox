using System;

namespace Jellyfin.Core;

/// <summary>
/// Describes a subtitle stream of a media source that is played by a native player.
/// </summary>
public sealed class NativeSubtitle
{
    /// <summary>
    /// Gets or sets the jellyfin stream index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the url of an external subtitle file. Null if the subtitle is embedded into the container.
    /// </summary>
    public Uri Url { get; set; }
}
