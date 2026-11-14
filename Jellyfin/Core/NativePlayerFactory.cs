using System;
using Jellyfin.Core.Contract;

namespace Jellyfin.Core;

/// <summary>
/// Creates native video players depending on the <see cref="PlayerMode"/>.
/// </summary>
public class NativePlayerFactory : INativePlayerFactory
{
    /// <inheritdoc />
    public INativeVideoPlayer Create(PlayerMode mode)
    {
        return mode switch
        {
            PlayerMode.Native => new NativeMediaPlayerEngine(),
            PlayerMode.Vlc => new VlcPlayerEngine(),
            _ => throw new NotSupportedException("The player mode " + mode + " is not a native player."),
        };
    }
}
