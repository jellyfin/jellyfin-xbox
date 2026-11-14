using System;
using System.Globalization;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Microsoft.Extensions.Logging;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Core;

/// <summary>
/// Bridges player.* messages of the web UI to a native video player and reports its events back.
/// </summary>
public sealed class NativePlayerBridge : INativePlayerBridge
{
    private readonly Frame _frame;
    private readonly INativePlayerFactory _factory;
    private readonly ILogger<NativePlayerBridge> _logger;
    private INativeVideoPlayer _player;
    private PlayerMode _playerMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativePlayerBridge"/> class.
    /// </summary>
    /// <param name="frame">The frame used to access the UI dispatcher.</param>
    /// <param name="factory">The player factory.</param>
    /// <param name="logger">The logger.</param>
    public NativePlayerBridge(Frame frame, INativePlayerFactory factory, ILogger<NativePlayerBridge> logger)
    {
        _frame = frame;
        _factory = factory;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<FrameworkElement> SurfaceChanged;

    /// <inheritdoc />
    public event EventHandler<string> MessageToWeb;

    /// <inheritdoc />
    public void HandleMessage(string type, JsonObject args)
    {
        _ = _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            try
            {
                await HandleOnUiThreadAsync(type, args).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Native player command {Type} failed", type);
                Send("error", null, ex.Message);
            }
        });
    }

    /// <inheritdoc />
    public void Reset()
    {
        _ = _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, DestroyPlayer);
    }

    private async Task HandleOnUiThreadAsync(string type, JsonObject args)
    {
        switch (type)
        {
            case "player.play":
                await PlayAsync(args).ConfigureAwait(true);
                break;
            case "player.pause":
                _player?.Pause();
                break;
            case "player.unpause":
                _player?.Resume();
                break;
            case "player.stop":
            case "player.destroy":
                DestroyPlayer();
                break;
            case "player.seek":
                _player?.Seek(TimeSpan.FromMilliseconds(args.GetNamedNumber("positionMs", 0)));
                break;
            case "player.volume":
                _player?.SetVolume(args.GetNamedNumber("volume", 100), args.GetNamedBoolean("muted", false));
                break;
            case "player.audioTrack":
                _player?.SetAudioStream((int)args.GetNamedNumber("index", -1));
                break;
            case "player.subtitleTrack":
                _player?.SetSubtitleStream((int)args.GetNamedNumber("index", -1));
                break;
            default:
                _logger.LogWarning("Unknown player message {Type}", type);
                break;
        }
    }

    private async Task PlayAsync(JsonObject args)
    {
        var mode = Central.Settings.PlayerMode;
        if (mode == PlayerMode.Web)
        {
            Send("error", null, "Native player is disabled");
            return;
        }

        var request = new NativePlayRequest
        {
            Url = new Uri(args.GetNamedString("url")),
            StartPosition = TimeSpan.FromMilliseconds(args.GetNamedNumber("startPositionMs", 0)),
            AudioStreamIndex = (int)args.GetNamedNumber("audioStreamIndex", -1),
            SubtitleStreamIndex = (int)args.GetNamedNumber("subtitleStreamIndex", -1),
        };

        foreach (var value in args.GetNamedArray("audioStreams", new JsonArray()))
        {
            request.AudioStreams.Add((int)value.GetNumber());
        }

        foreach (var value in args.GetNamedArray("subtitles", new JsonArray()))
        {
            var item = value.GetObject();
            var url = item.GetNamedString("url", string.Empty);
            request.Subtitles.Add(new NativeSubtitle
            {
                Index = (int)item.GetNamedNumber("index"),
                Url = string.IsNullOrEmpty(url) ? null : new Uri(url),
            });
        }

        if (_player == null || _playerMode != mode)
        {
            DestroyPlayer();
            _playerMode = mode;
            _player = _factory.Create(mode);
            _player.EventRaised += OnPlayerEvent;
            SurfaceChanged?.Invoke(this, _player.Surface);
        }

        await _player.PlayAsync(request).ConfigureAwait(true);
    }

    private void DestroyPlayer()
    {
        var player = _player;
        if (player == null)
        {
            return;
        }

        _player = null;
        player.EventRaised -= OnPlayerEvent;
        player.Stop();
        SurfaceChanged?.Invoke(this, null);
        player.Dispose();
    }

    private void OnPlayerEvent(object sender, NativePlayerEventArgs e)
    {
        Send(e.Name, e, e.Message);
    }

    private void Send(string name, NativePlayerEventArgs e, string message)
    {
        var args = new JsonObject
        {
            ["name"] = JsonValue.CreateStringValue(name),
            ["positionMs"] = JsonValue.CreateNumberValue(e?.PositionMs ?? 0),
            ["durationMs"] = JsonValue.CreateNumberValue(e?.DurationMs ?? 0),
            ["paused"] = JsonValue.CreateBooleanValue(e?.IsPaused ?? false),
            ["volume"] = JsonValue.CreateNumberValue(e?.Volume ?? 0),
            ["muted"] = JsonValue.CreateBooleanValue(e?.IsMuted ?? false),
            ["message"] = JsonValue.CreateStringValue(message ?? string.Empty),
        };
        var root = new JsonObject
        {
            ["type"] = JsonValue.CreateStringValue("player.event"),
            ["args"] = args,
        };
        MessageToWeb?.Invoke(this, root.Stringify());
    }
}
