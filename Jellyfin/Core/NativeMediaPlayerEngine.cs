using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Core;

/// <summary>
/// Plays video using the native Windows.Media.Playback.MediaPlayer.
/// </summary>
public sealed class NativeMediaPlayerEngine : INativeVideoPlayer
{
    private readonly MediaPlayerElement _element;
    private readonly DispatcherTimer _timer;
    private MediaPlayer _player;
    private MediaSource _source;
    private MediaPlaybackItem _item;
    private NativePlayRequest _request;
    private bool _ended;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeMediaPlayerEngine"/> class.
    /// </summary>
    public NativeMediaPlayerEngine()
    {
        _element = new MediaPlayerElement
        {
            AreTransportControlsEnabled = false,
            Stretch = Windows.UI.Xaml.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = false,
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Raise("timeupdate");
    }

    /// <inheritdoc />
    public event EventHandler<NativePlayerEventArgs> EventRaised;

    /// <inheritdoc />
    public FrameworkElement Surface => _element;

    /// <inheritdoc />
    public Task PlayAsync(NativePlayRequest request)
    {
        Stop();
        _request = request;
        _ended = false;

        _source = MediaSource.CreateFromUri(request.Url);
        foreach (var subtitle in request.Subtitles.Where(e => e.Url != null))
        {
            var index = subtitle.Index;
            var textSource = TimedTextSource.CreateFromUri(subtitle.Url);
            textSource.Resolved += (_, args) =>
            {
                if (args.Tracks.Count > 0)
                {
                    args.Tracks[0].Label = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            };
            _source.ExternalTimedTextSources.Add(textSource);
        }

        _item = new MediaPlaybackItem(_source);
        _player = new MediaPlayer { AutoPlay = true };
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.VolumeChanged += (_, _) => Raise("volumechange");
        _player.Source = _item;
        _element.SetMediaPlayer(_player);
        _timer.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Pause()
    {
        _player?.Pause();
    }

    /// <inheritdoc />
    public void Resume()
    {
        _player?.Play();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _timer.Stop();
        if (_player == null)
        {
            return;
        }

        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.Pause();
        _element.SetMediaPlayer(null);
        _player.Source = null;
        _player.Dispose();
        _player = null;
        _item?.Source?.Dispose();
        _source = null;
        _item = null;
    }

    /// <inheritdoc />
    public void Seek(TimeSpan position)
    {
        if (_player != null)
        {
            _player.PlaybackSession.Position = position;
            Raise("timeupdate");
        }
    }

    /// <inheritdoc />
    public void SetVolume(double volume, bool muted)
    {
        if (_player != null)
        {
            _player.Volume = Math.Clamp(volume / 100d, 0d, 1d);
            _player.IsMuted = muted;
        }
    }

    /// <inheritdoc />
    public void SetAudioStream(int jellyfinIndex)
    {
        if (_item == null || _request == null)
        {
            return;
        }

        var ordinal = _request.AudioStreams.IndexOf(jellyfinIndex);
        if (ordinal >= 0 && ordinal < _item.AudioTracks.Count)
        {
            _item.AudioTracks.SelectedIndex = ordinal;
        }
    }

    /// <inheritdoc />
    public void SetSubtitleStream(int jellyfinIndex)
    {
        if (_item == null || _request == null)
        {
            return;
        }

        var tracks = _item.TimedMetadataTracks;
        var subtitleTracks = Enumerable.Range(0, tracks.Count)
            .Where(i => tracks[i].TimedMetadataKind == TimedMetadataKind.Subtitle || tracks[i].TimedMetadataKind == TimedMetadataKind.Caption)
            .ToArray();

        foreach (var i in subtitleTracks)
        {
            tracks.SetPresentationMode((uint)i, TimedMetadataTrackPresentationMode.Disabled);
        }

        var subtitle = _request.Subtitles.FirstOrDefault(e => e.Index == jellyfinIndex);
        if (subtitle == null)
        {
            return;
        }

        int target = -1;
        if (subtitle.Url != null)
        {
            var label = jellyfinIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            target = subtitleTracks.Where(i => tracks[i].Label == label).Select(i => (int?)i).FirstOrDefault() ?? -1;
        }
        else
        {
            var embedded = _request.Subtitles.Where(e => e.Url == null).ToList();
            var ordinal = embedded.IndexOf(subtitle);
            var embeddedTracks = subtitleTracks.Where(i => !_request.Subtitles.Any(e => e.Url != null && tracks[i].Label == e.Index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
            if (ordinal >= 0 && ordinal < embeddedTracks.Length)
            {
                target = embeddedTracks[ordinal];
            }
        }

        if (target >= 0)
        {
            tracks.SetPresentationMode((uint)target, TimedMetadataTrackPresentationMode.PlatformPresented);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        _ = _element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        {
            if (_request == null || _player == null)
            {
                return;
            }

            if (_request.StartPosition > TimeSpan.Zero)
            {
                _player.PlaybackSession.Position = _request.StartPosition;
            }

            SetAudioStream(_request.AudioStreamIndex);
            SetSubtitleStream(_request.SubtitleStreamIndex);
            Raise("opened");
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        _ended = true;
        Raise("ended");
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Raise("error", args.Error + ": " + args.ErrorMessage + " (0x" + args.ExtendedErrorCode?.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + ")");
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        if (_ended)
        {
            return;
        }

        switch (sender.PlaybackState)
        {
            case MediaPlaybackState.Paused:
                Raise("pause");
                break;
            case MediaPlaybackState.Playing:
                Raise("unpause");
                break;
            default:
                break;
        }
    }

    private void Raise(string name, string message = null)
    {
        var player = _player;
        if (player == null)
        {
            return;
        }

        try
        {
            var session = player.PlaybackSession;
            EventRaised?.Invoke(this, new NativePlayerEventArgs(name)
            {
                PositionMs = session.Position.TotalMilliseconds,
                DurationMs = session.NaturalDuration.TotalMilliseconds,
                IsPaused = session.PlaybackState == MediaPlaybackState.Paused,
                Volume = player.Volume * 100d,
                IsMuted = player.IsMuted,
                Message = message,
            });
        }
        catch (ObjectDisposedException)
        {
            // player was disposed while an event was in flight
        }
    }
}
