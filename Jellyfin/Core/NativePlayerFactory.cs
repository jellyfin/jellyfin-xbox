using System;
using System.Runtime.CompilerServices;
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
            PlayerMode.Vlc => CreateVlcPlayer(),
            _ => throw new NotSupportedException("The player mode " + mode + " is not a native player."),
        };
    }

    // Kept in its own non-inlined method so the JIT only touches VlcPlayerEngine (and with it the LibVLC assemblies)
    // when the VLC mode is actually selected.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static INativeVideoPlayer CreateVlcPlayer()
    {
        return new VlcPlayerEngine();
    }
}
