using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Windows.UI.Xaml;

namespace Jellyfin.Core;

/// <summary>
/// Plays video using LibVLC. The view must be added to the visual tree before <see cref="PlayAsync"/> completes.
/// </summary>
public sealed class VlcPlayerEngine : INativeVideoPlayer
{
    private readonly VideoView _view;
    private readonly TaskCompletionSource<bool> _initialized = new TaskCompletionSource<bool>();
    private LibVLC _libVlc;
    private LibVLCSharp.Shared.MediaPlayer _player;
    private NativePlayRequest _request;
    private bool _disposed;
    private bool _opened;

    /// <summary>
    /// Initializes a new instance of the <see cref="VlcPlayerEngine"/> class.
    /// </summary>
    public VlcPlayerEngine()
    {
        _view = new VideoView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = false,
        };
        _view.Initialized += OnViewInitialized;
    }

    /// <inheritdoc />
    public event EventHandler<NativePlayerEventArgs> EventRaised;

    /// <inheritdoc />
    public FrameworkElement Surface => _view;

    /// <inheritdoc />
    public async Task PlayAsync(NativePlayRequest request)
    {
        await _initialized.Task;
        if (_disposed)
        {
            return;
        }

        _request = request;
        using var media = new Media(_libVlc, request.Url);
        foreach (var subtitle in request.Subtitles.Where(e => e.Url != null))
        {
            media.AddSlave(MediaSlaveType.Subtitle, 4, subtitle.Url.AbsoluteUri);
        }

        if (request.StartPosition > TimeSpan.Zero)
        {
            media.AddOption(":start-time=" + request.StartPosition.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        _player.Play(media);
    }

    /// <inheritdoc />
    public void Pause()
    {
        _player?.SetPause(true);
    }

    /// <inheritdoc />
    public void Resume()
    {
        _player?.SetPause(false);
    }

    /// <inheritdoc />
    public void Stop()
    {
        _player?.Stop();
    }

    /// <inheritdoc />
    public void Seek(TimeSpan position)
    {
        if (_player != null)
        {
            _player.Time = (long)position.TotalMilliseconds;
        }
    }

    /// <inheritdoc />
    public void SetVolume(double volume, bool muted)
    {
        if (_player != null)
        {
            _player.Volume = (int)Math.Clamp(volume, 0d, 100d);
            _player.Mute = muted;
        }
    }

    /// <inheritdoc />
    public void SetAudioStream(int jellyfinIndex)
    {
        if (_player == null || _request == null)
        {
            return;
        }

        var ordinal = _request.AudioStreams.IndexOf(jellyfinIndex);
        var tracks = _player.AudioTrackDescription.Where(e => e.Id >= 0).ToArray();
        if (ordinal >= 0 && ordinal < tracks.Length)
        {
            _player.SetAudioTrack(tracks[ordinal].Id);
        }
    }

    /// <inheritdoc />
    public void SetSubtitleStream(int jellyfinIndex)
    {
        if (_player == null || _request == null)
        {
            return;
        }

        var subtitle = _request.Subtitles.FirstOrDefault(e => e.Index == jellyfinIndex);
        if (subtitle == null)
        {
            _player.SetSpu(-1);
            return;
        }

        var tracks = _player.SpuDescription.Where(e => e.Id >= 0).ToArray();
        var ordinal = _request.Subtitles.IndexOf(subtitle);
        if (ordinal >= 0 && ordinal < tracks.Length)
        {
            _player.SetSpu(tracks[ordinal].Id);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _view.Initialized -= OnViewInitialized;
        _initialized.TrySetResult(false);
        if (_player != null)
        {
            _player.Playing -= OnPlaying;
            _player.Paused -= OnPaused;
            _player.EndReached -= OnEnded;
            _player.EncounteredError -= OnError;
            _player.TimeChanged -= OnTimeChanged;
            _player.VolumeChanged -= OnVolumeChanged;
            _player.Stop();
            _view.MediaPlayer = null;
            _player.Dispose();
            _player = null;
        }

        _libVlc?.Dispose();
        _libVlc = null;
    }

    private void OnViewInitialized(object sender, InitializedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _libVlc = new LibVLC(e.SwapChainOptions);
        _player = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
        _player.Playing += OnPlaying;
        _player.Paused += OnPaused;
        _player.EndReached += OnEnded;
        _player.EncounteredError += OnError;
        _player.TimeChanged += OnTimeChanged;
        _player.VolumeChanged += OnVolumeChanged;
        _view.MediaPlayer = _player;
        _initialized.TrySetResult(true);
    }

    private void OnPlaying(object sender, EventArgs e)
    {
        if (_request != null && _player != null && _player.Length > 0 && !_opened)
        {
            _opened = true;
            SetAudioStream(_request.AudioStreamIndex);
            SetSubtitleStream(_request.SubtitleStreamIndex);
            Raise("opened");
        }
        else
        {
            Raise("unpause");
        }
    }

    private void OnPaused(object sender, EventArgs e) => Raise("pause");

    private void OnEnded(object sender, EventArgs e) => Raise("ended");

    private void OnError(object sender, EventArgs e) => Raise("error", "VLC playback error");

    private void OnTimeChanged(object sender, MediaPlayerTimeChangedEventArgs e) => Raise("timeupdate");

    private void OnVolumeChanged(object sender, MediaPlayerVolumeChangedEventArgs e) => Raise("volumechange");

    private void Raise(string name, string message = null)
    {
        var player = _player;
        if (player == null || _disposed)
        {
            return;
        }

        EventRaised?.Invoke(this, new NativePlayerEventArgs(name)
        {
            PositionMs = player.Time,
            DurationMs = player.Length,
            IsPaused = !player.IsPlaying,
            Volume = player.Volume,
            IsMuted = player.Mute,
            Message = message,
        });
    }
}
