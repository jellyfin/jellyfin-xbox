using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Abstraction of a native video playback engine. All members must be used from the UI thread.
/// </summary>
public interface INativeVideoPlayer : IDisposable
{
    /// <summary>
    /// Raised when the player state changes or progress is reported. Can be raised from any thread.
    /// </summary>
    event EventHandler<NativePlayerEventArgs> EventRaised;

    /// <summary>
    /// Gets the element the video is rendered to.
    /// </summary>
    FrameworkElement Surface { get; }

    /// <summary>
    /// Starts playback.
    /// </summary>
    /// <param name="request">The play request.</param>
    /// <returns>A task that completes when playback was started.</returns>
    Task PlayAsync(NativePlayRequest request);

    /// <summary>
    /// Pauses playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes playback.
    /// </summary>
    void Resume();

    /// <summary>
    /// Stops playback.
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to the given position.
    /// </summary>
    /// <param name="position">The new position.</param>
    void Seek(TimeSpan position);

    /// <summary>
    /// Sets the volume.
    /// </summary>
    /// <param name="volume">Volume in the range of 0 to 100.</param>
    /// <param name="muted">Whether to mute.</param>
    void SetVolume(double volume, bool muted);

    /// <summary>
    /// Selects an audio stream.
    /// </summary>
    /// <param name="jellyfinIndex">The jellyfin stream index.</param>
    void SetAudioStream(int jellyfinIndex);

    /// <summary>
    /// Selects a subtitle stream.
    /// </summary>
    /// <param name="jellyfinIndex">The jellyfin stream index or -1 to disable subtitles.</param>
    void SetSubtitleStream(int jellyfinIndex);
}
