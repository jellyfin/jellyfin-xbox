(function (appName, appVersion, deviceName, supportsHdr10, supportsDolbyVision, supports4k, forceHevcSupport) {
    'use strict';

    console.log('Windows UWP adapter');

    const xbox = deviceName.toLowerCase().indexOf('xbox') !== -1;
    const mobile = deviceName.toLowerCase().indexOf('mobile') !== -1;

    console.debug('NativeShell capability flags', {
        deviceName,
        supportsHdr10,
        supportsDolbyVision,
        supports4k,
        forceHevcSupport
    });

    function detectHevcSupport() {
        const videoElement = document.createElement('video');
        if (!videoElement || !videoElement.canPlayType) {
            return false;
        }

        return [
            'video/mp4; codecs="hvc1.1.L120"',
            'video/mp4; codecs="hev1.1.L120"',
            'video/mp4; codecs="hvc1.1.0.L120"',
            'video/mp4; codecs="hev1.1.0.L120"'
        ].some(codec => videoElement.canPlayType(codec).replace(/no/, ''));
    }

    function postMessage(type, args = {}) {
        console.debug(`AppHost.${type}`, args);
        const payload = {
            'type': type,
            'args': args
        };

        window.chrome.webview.postMessage(JSON.stringify(payload));
    }

    const AppInfo = {
        deviceName: deviceName,
        appName: appName,
        appVersion: appVersion
    };

    // List of supported features
    const SupportedFeatures = [
        'displaylanguage',
        'displaymode',
        'exit',
        'exitmenu',
        'externallinkdisplay',
        'externallinks',
        'htmlaudioautoplay',
        'htmlvideoautoplay',
        'multiserver',
        'otherapppromotions',
        'screensaver',
        'subtitleappearancesettings',
        'subtitleburnsettings',
        'targetblank'
    ];

    if (xbox || mobile) {
        SupportedFeatures.push('physicalvolumecontrol');
    }

    SupportedFeatures.push('clientsettings');

    console.debug('SupportedFeatures', SupportedFeatures);

    window.NativeShell = {
        AppHost: {
            init: function () {
                console.debug('AppHost.init', AppInfo);
                return Promise.resolve(AppInfo);
            },

            appName: function () {
                console.debug('AppHost.appName', AppInfo.appName);
                return AppInfo.appName;
            },

            appVersion: function () {
                console.debug('AppHost.appVersion', AppInfo.appVersion);
                return AppInfo.appVersion;
            },

            deviceName: function () {
                console.debug('AppHost.deviceName', AppInfo.deviceName);
                return AppInfo.deviceName;
            },

            exit: function () {
                postMessage('exit');
            },

            getDefaultLayout: function () {
                let layout;
                if (xbox) {
                    layout = 'tv';
                } else if (mobile) {
                    layout = 'mobile';
                } else {
                    layout = 'desktop';
                }
                console.debug('AppHost.getDefaultLayout', layout);
                return layout;
            },

            getDeviceProfile: function (profileBuilder) {
                console.debug('AppHost.getDeviceProfile');
                const options = {};
                if (forceHevcSupport === true || detectHevcSupport()) {
                    options.supportsHevc = true;
                }
                if (supportsHdr10 != null) {
                    options.supportsHdr10 = supportsHdr10;
                }
                if (supportsDolbyVision != null) {
                    options.supportsDolbyVision = supportsDolbyVision;
                }
                if (xbox) {
                    // MSE cannot decode AC3 in HLS fMP4 despite WebView2 reporting support.
                    options.disableHlsVideoAudioCodecs = ['ac3', 'eac3'];
                    if (supports4k === true) {
                        options.maxVideoWidth = 3840;
                    }
                }
                console.debug('AppHost.getDeviceProfile.options', options);
                return profileBuilder(options);
            },

            supports: function (command) {
                const isSupported = command && SupportedFeatures.indexOf(command.toLowerCase()) !== -1;
                console.debug('AppHost.supports', {
                    command: command,
                    isSupported: isSupported
                });
                return isSupported;
            }
        },

        enableFullscreen: function (videoInfo) {
        },

        disableFullscreen: function () {
            postMessage('disableFullscreen');
        },

        getPlugins: function () {
            console.debug('getPlugins');
            postMessage('loaded');
            return UWP_NATIVE_PLAYER_ENABLED ? ["UwpXboxHdmiSetupPlugin", "UwpNativeVideoPlayer"] : ["UwpXboxHdmiSetupPlugin"];
        },

        selectServer: function () {
            postMessage('selectServer');
        },

        openClientSettings: function () {
            postMessage('openClientSettings');
        }
    };
})(APP_NAME, APP_VERSION, DEVICE_NAME, SUPPORTS_HDR, SUPPORTS_DOVI, SUPPORTS_4K, FORCE_HEVC_SUPPORT);


/**
 * Plugin build to toggle attached HDMI monitors
 * Follows: https://github.com/jellyfin/jellyfin-web/blob/master/src/types/plugin.ts
 */
class UwpXboxHdmiSetupPlugin {
    constructor(pluginOptions) {
        this.name = "UwpXboxHdmiSetupPlugin";
        this.id = "UwpXboxHdmiSetupPlugin";
        this.type = "preplayintercept";
        this.priority = 0;
        this.PluginOptions = pluginOptions;
    }

    async intercept(options) {
        const item = options.item;
        if (!item) {
            return;
        }
        const mediaSourceId = options.mediaSourceId || item.MediaSourceId;
        let mediaStreams = null;
        let mediaSource = null;

        if (item.MediaSources == null || item.MediaSources.length === 0) {
            const apiClient = this.PluginOptions.ServerConnections.getApiClient(item.ServerId);
            const isLiveTv = ["TvChannel", "LiveTvChannel"].includes(item.Type);
            if (!isLiveTv) {
                const fullItem = await apiClient.getItem(apiClient.getCurrentUserId(), item.Id);
                if (fullItem?.MediaSources?.length > 0) {
                    mediaSource = fullItem.MediaSources.find(e => e.Id === mediaSourceId) || fullItem.MediaSources[0];
                    mediaStreams = mediaSource?.MediaStreams || null;
                }
                else {
                    mediaSource = fullItem;
                    mediaStreams = fullItem?.MediaStreams || null;
                }
            }
        }
        else {
            mediaSource = item.MediaSources.find(e => e.Id === mediaSourceId) || item.MediaSources[0];
            mediaStreams = mediaSource?.MediaStreams || null;
        }

        if (mediaStreams == null || mediaStreams.length == 0) {
            return;
        }

        const stream = mediaStreams.find(s => s.Type === 'Video');

        if (stream == null) {
            return;
        }

        const payload = {
            'type': "enableFullscreen",
            'args': {
                'videoWidth': stream.Width,
                'videoHeight': stream.Height,
                'videoFrameRate': (stream.AverageFrameRate || stream.RealFrameRate),
                'videoRangeType': stream.VideoRangeType
            }
        };

        console.debug('UwpXboxHdmiSetupPlugin.enableFullscreen', payload.args);
        window.chrome.webview.postMessage(JSON.stringify(payload));
        await new Promise(resolve => setTimeout(resolve, 3000)); // wait 3 sec before continuing with playback to setup display
    }
}

window["UwpXboxHdmiSetupPlugin"] = async () => UwpXboxHdmiSetupPlugin;

/**
 * Makes the jellyfin-web page transparent while a native video surface is rendered behind the WebView2,
 * so only the web OSD is drawn on top of the native video. Driven by 'host.nativeSurfaceChanged'
 * messages that are posted by the UWP host (JellyfinWebViewModel).
 */
const UwpNativeSurfaceTransparency = (() => {
    const styleElementId = 'uwp-native-surface-transparency';
    const transparentSelectors = [
        'html',
        'body',
        '.skinBody',
        '.backgroundContainer',
        '.backdropContainer',
        '.videoOsdPage',
        '#videoOsdPage'
    ];

    function enableTransparentPageBackground() {
        let styleElement = document.getElementById(styleElementId);
        if (!styleElement) {
            styleElement = document.createElement('style');
            styleElement.id = styleElementId;
            document.head.appendChild(styleElement);
        }

        styleElement.textContent = transparentSelectors.join(',')
            + '{background:transparent !important;background-color:transparent !important;}';
    }

    function disableTransparentPageBackground() {
        const styleElement = document.getElementById(styleElementId);
        if (styleElement) {
            styleElement.remove();
        }
    }

    function onHostMessage(event) {
        let message = event.data;
        if (typeof message === 'string') {
            try {
                message = JSON.parse(message);
            } catch {
                return;
            }
        }

        if (!message || message.type !== 'host.nativeSurfaceChanged') {
            return;
        }

        if (message.args && message.args.visible) {
            enableTransparentPageBackground();
        } else {
            disableTransparentPageBackground();
        }
    }

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', onHostMessage);
    }

    return { enableTransparentPageBackground, disableTransparentPageBackground };
})();

const UWP_NATIVE_PLAYER_ENABLED = 'PLAYER_MODE' !== 'Web';

// Identity of the player as it is shown in the jellyfin-web playback info and logs.
// The configured mode is injected by the host (Native = Windows MediaPlayer, Vlc = LibVLC).
const uwpConfiguredPlayerMode = 'PLAYER_MODE';
const uwpPlayerIdentity = uwpConfiguredPlayerMode === 'Vlc'
    ? { id: 'uwpvlcvideoplayer', name: 'VLC Player (UWP)' }
    : { id: 'uwpmediaplayervideoplayer', name: 'Windows Media Player (UWP)' };

/**
 * Media player plugin that delegates playback to the native UWP host (MediaPlayer or VLC).
 * Follows the contract of src/plugins/htmlVideoPlayer/plugin.js.
 */
class UwpNativeVideoPlayer {
    constructor(pluginOptions) {
        this.name = uwpPlayerIdentity.name;
        this.id = uwpPlayerIdentity.id;
        this.type = 'mediaplayer';
        this.mediaType = 'Video';
        this.priority = -1;
        this.isLocalPlayer = true;
        this.PluginOptions = pluginOptions;
        this._events = pluginOptions.events;
        this._src = null;
        this._positionMs = 0;
        this._durationMs = 0;
        this._paused = false;
        this._volume = 100;
        this._muted = false;
        this._playResolve = null;
        this._playReject = null;
        this._subtitles = [];
        this._onHostMessage = (e) => this._handleHostMessage(e.data);
        window.chrome.webview.addEventListener('message', this._onHostMessage);
    }

    _post(type, args = {}) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'player.' + type, args: args }));
    }

    _trigger(name, args) {
        this._events.trigger(this, name, args || []);
    }

    _handleHostMessage(message) {
        if (typeof message === 'string') {
            try { message = JSON.parse(message); } catch { return; }
        }
        if (!message || message.type !== 'player.event' || !this._src) {
            return;
        }
        const a = message.args;
        this._positionMs = a.positionMs;
        if (a.durationMs > 0) {
            this._durationMs = a.durationMs;
        }
        switch (a.name) {
            case 'opened':
                this._volume = a.volume;
                this._muted = a.muted;
                if (this._playResolve) {
                    this._playResolve();
                    this._playResolve = null;
                    this._playReject = null;
                    const appRouter = this.PluginOptions.appRouter;
                    if (this._openOsd && appRouter && appRouter.showVideoOsd) {
                        this._openOsd = false;
                        // wait until playbackmanager has registered this player as the current player
                        setTimeout(() => {
                            Promise.resolve(appRouter.showVideoOsd()).catch(err => console.error('showVideoOsd failed', err));
                        }, 100);
                    }
                }
                this._paused = false;
                this._trigger('unpause');
                break;
            case 'timeupdate':
                this._trigger('timeupdate');
                break;
            case 'pause':
                if (!this._paused) {
                    this._paused = true;
                    this._trigger('pause');
                }
                break;
            case 'unpause':
                if (this._paused) {
                    this._paused = false;
                    this._trigger('unpause');
                }
                break;
            case 'volumechange':
                this._volume = a.volume;
                this._muted = a.muted;
                this._trigger('volumechange');
                break;
            case 'ended':
                this._finish();
                break;
            case 'error':
                console.error('Native player error', a.message);
                if (this._playReject) {
                    this._playReject(new Error(a.message));
                    this._playResolve = null;
                    this._playReject = null;
                    this._src = null;
                } else {
                    this._trigger('error', [{ type: 'mediadecodeerror' }]);
                }
                break;
        }
    }

    _finish() {
        const src = this._src;
        this._src = null;
        this._post('stop');
        this._trigger('stopped', [{ src: src }]);
    }

    currentSrc() { return this._src; }

    canPlayMediaType(mediaType) { return (mediaType || '').toLowerCase() === 'video'; }

    canPlayItem() { return true; }

    canPlayUrl() { return true; }

    getDeviceProfile() {
        return Promise.resolve({
            Name: uwpPlayerIdentity.name,
            MaxStreamingBitrate: 120000000,
            MaxStaticBitrate: 400000000,
            MusicStreamingTranscodingBitrate: 384000,
            DirectPlayProfiles: [{ Type: 'Video' }, { Type: 'Audio' }],
            TranscodingProfiles: [{
                Container: 'ts', Type: 'Video', AudioCodec: 'aac,mp3,ac3,eac3',
                VideoCodec: 'h264,hevc', Context: 'Streaming', Protocol: 'hls',
                MaxAudioChannels: '6', MinSegments: '1', BreakOnNonKeyFrames: true
            }],
            ContainerProfiles: [],
            CodecProfiles: [],
            SubtitleProfiles: [
                { Format: 'srt', Method: 'External' },
                { Format: 'vtt', Method: 'External' },
                { Format: 'ass', Method: 'External' },
                { Format: 'ssa', Method: 'External' },
                { Format: 'srt', Method: 'Embed' },
                { Format: 'ass', Method: 'Embed' },
                { Format: 'ssa', Method: 'Embed' },
                { Format: 'subrip', Method: 'Embed' },
                { Format: 'pgssub', Method: 'Embed' },
                { Format: 'dvdsub', Method: 'Embed' }
            ]
        });
    }

    getSupportedFeatures() { return ['SetAspectRatio']; }

    supports() { return false; }

    play(options) {
        this._src = options.url;
        this._positionMs = 0;
        this._durationMs = 0;
        this._paused = false;

        const streams = (options.mediaSource && options.mediaSource.MediaStreams) || [];
        const apiClient = options.item && this.PluginOptions.ServerConnections.getApiClient(options.item.ServerId);
        const audioStreams = streams.filter(s => s.Type === 'Audio').map(s => s.Index);
        const subtitles = streams.filter(s => s.Type === 'Subtitle').map(s => {
            const external = s.DeliveryMethod === 'External' && s.DeliveryUrl;
            return { index: s.Index, url: external ? (apiClient ? apiClient.getUrl(s.DeliveryUrl) : s.DeliveryUrl) : '' };
        });
        this._subtitles = subtitles;

        const ticks = options.playerStartPositionTicks || 0;
        this._openOsd = !!options.fullscreen;
        return new Promise((resolve, reject) => {
            this._playResolve = resolve;
            this._playReject = reject;
            this._post('play', {
                url: options.url,
                startPositionMs: ticks / 10000,
                audioStreamIndex: options.mediaSource && options.mediaSource.DefaultAudioStreamIndex != null ? options.mediaSource.DefaultAudioStreamIndex : -1,
                subtitleStreamIndex: options.mediaSource && options.mediaSource.DefaultSubtitleStreamIndex != null ? options.mediaSource.DefaultSubtitleStreamIndex : -1,
                audioStreams: audioStreams,
                subtitles: subtitles
            });
        });
    }

    stop(destroyPlayer) {
        const src = this._src;
        this._src = null;
        this._post(destroyPlayer ? 'destroy' : 'stop');
        if (src) {
            this._trigger('stopped', [{ src: src }]);
        }
        return Promise.resolve();
    }

    destroy() {
        this._post('destroy');
    }

    pause() { this._post('pause'); }

    unpause() { this._post('unpause'); }

    paused() { return this._paused; }

    currentTime(val) {
        if (val != null) {
            this._positionMs = val;
            this._post('seek', { positionMs: val });
            return;
        }
        return this._positionMs;
    }

    duration() { return this._durationMs > 0 ? this._durationMs : null; }

    seekable() { return this._durationMs > 0; }

    getBufferedRanges() { return []; }

    volume(val) {
        if (val != null) {
            this._volume = val;
            this._post('volume', { volume: val, muted: this._muted });
            return;
        }
        return this._volume;
    }

    getVolume() { return this._volume; }

    setVolume(val) { this.volume(val); }

    volumeUp() { this.volume(Math.min(100, this._volume + 2)); }

    volumeDown() { this.volume(Math.max(0, this._volume - 2)); }

    isMuted() { return this._muted; }

    setMute(mute) {
        this._muted = !!mute;
        this._post('volume', { volume: this._volume, muted: this._muted });
    }

    setAudioStreamIndex(index) { this._post('audioTrack', { index: index }); }

    setSubtitleStreamIndex(index) { this._post('subtitleTrack', { index: index }); }

    canSetAudioStreamIndex() { return true; }

    setSubtitleOffset() { }

    getSubtitleOffset() { return 0; }

    getStats() { return Promise.resolve({ categories: [] }); }

    setAspectRatio() { }

    getAspectRatio() { return 'auto'; }

    getSupportedAspectRatios() { return []; }

    resetSubtitleOffset() { }

    enableShowingSubtitleOffset() { }

    disableShowingSubtitleOffset() { }

    isShowingSubtitleOffsetEnabled() { return false; }
}

window["UwpNativeVideoPlayer"] = async () => UwpNativeVideoPlayer;

if (!window.consoleXboxOverride)
{
    window.consoleXboxOverride = true;
    const logOverride = function(logLevel) {
        let oldLogLevel = console[logLevel];
        console[logLevel] = function () {
            oldLogLevel.apply(console, arguments);
            let argsArray = Array.from(arguments);
            window.chrome.webview.postMessage(JSON.stringify({ type: "log", args: { level: logLevel, messages: argsArray } }));
        }
    }
    // debug is intentionally commented out as it can overwhelm the interopt layer. Uncomment for troubleshooting if needed.
    //logOverride("debug");
    logOverride("error");
    logOverride("log");
    logOverride("warn");
    logOverride("info");
}

(() => {
  if (window.__uwpStorageHookInstalled) return;
  window.__uwpStorageHookInstalled = true;

  const post = (key, value) => {
    window.chrome.webview.postMessage(JSON.stringify({
      type: "updateAccessToken",
      args: { accessToken: value ?? "" }
    }));
  };

  const origSetItem = Storage.prototype.setItem;
  const origRemoveItem = Storage.prototype.removeItem;
  const origClear = Storage.prototype.clear;

  Storage.prototype.setItem = function (key, value) {
    origSetItem.apply(this, arguments);
    if (key === "access_token") post(key, value);
    if (key === "jellyfin_credentials") {
      try { post(key, JSON.parse(value)?.Servers?.[0]?.AccessToken ?? ""); } catch { post(key, ""); }
    }
  };

  Storage.prototype.removeItem = function (key) {
    origRemoveItem.apply(this, arguments);
    if (key === "access_token" || key === "jellyfin_credentials") post(key, "");
  };

  Storage.prototype.clear = function () {
    origClear.apply(this, arguments);
    post("access_token", "");
  };
})();
