(function (appName, appVersion, deviceName, supportsHdr10, supportsDolbyVision) {
    'use strict';

    console.log('Windows UWP adapter');

    const xbox = deviceName.toLowerCase().indexOf('xbox') !== -1;
    const xboxSeries = deviceName.toLowerCase().indexOf('xbox series') !== -1;
    const mobile = deviceName.toLowerCase().indexOf('mobile') !== -1;

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
                if (supportsHdr10 != null) {
                    options.supportsHdr10 = supportsHdr10;
                }
                if (supportsDolbyVision != null) {
                    options.supportsDolbyVision = supportsDolbyVision;
                }
                if (xbox) {
                    // MSE cannot decode AC3 in HLS fMP4 despite WebView2 reporting support.
                    options.disableHlsVideoAudioCodecs = ['ac3', 'eac3'];
                    if (xboxSeries) {
                        options.maxVideoWidth = 3840;
                    }
                }
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
            return ["UwpXboxHdmiSetupPlugin", "UwpTrailerPlayer"];
        },

        selectServer: function () {
            postMessage('selectServer');
        },

        openClientSettings: function () {
            postMessage('openClientSettings');
        }
    };
})(APP_NAME, APP_VERSION, DEVICE_NAME, SUPPORTS_HDR, SUPPORTS_DOVI);


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
        if ("mediaSourceId" in options) {
            // Remote trailers have no library media source to probe.
            if (item.Url && !item.Id) {
                return;
            }

            const mediaSourceid = options.mediaSourceId;
            var mediaStreams = null;
            var mediaSource = null;

            if (item.MediaSources == null) {
                const apiClient = this.PluginOptions.ServerConnections.getApiClient(item.ServerId);
                const isLiveTv = ["TvChannel", "LiveTvChannel"].includes(item.Type);
                mediaStreams = isLiveTv ? null : await apiClient.getItem(apiClient.getCurrentUserId(), mediaSourceid || item.Id)
                    .then(fullItem => {
                        mediaSource = fullItem;
                        return fullItem.MediaStreams;
                    });
            }
            else {
                mediaSource = item.MediaSources.find(e => e.Id == mediaSourceid);
                if (mediaSource == null) {
                    return;
                }
                mediaStreams = mediaSource.MediaStreams;
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

            window.chrome.webview.postMessage(JSON.stringify(payload));
            await new Promise(resolve => setTimeout(resolve, 3000)); // wait 3 sec before continuing with playback to setup display
        }
    }
}

window["UwpXboxHdmiSetupPlugin"] = async () => UwpXboxHdmiSetupPlugin;

/**
 * In-app fullscreen trailer playback for YouTube, Vimeo, and direct video URLs.
 */
class UwpTrailerPlayer {
    constructor(pluginOptions) {
        this.name = 'UWP Trailer Player';
        this.type = 'mediaplayer';
        this.id = 'uwptrailerplayer';
        this.priority = -1;
        this.isLocalPlayer = true;
        this.PluginOptions = pluginOptions;
        this._currentSrc = null;
        this._started = false;
        this._active = false;
        this._container = null;
        this._mediaElement = null;
        this._ytPlayer = null;
        this._timeUpdateInterval = null;
        this.patchPlayTrailers();
    }

    patchPlayTrailers() {
        const playbackManager = this.PluginOptions?.playbackManager;
        if (!playbackManager || playbackManager._uwpTrailerPatchApplied) {
            return;
        }

        playbackManager._uwpTrailerPatchApplied = true;
        const originalPlayTrailers = playbackManager.playTrailers.bind(playbackManager);

        playbackManager.playTrailers = async (item) => {
            try {
                if (await this.playTrailersInApp(item)) {
                    return;
                }
            } catch (error) {
                console.error('In-app trailer playback failed', error);
                this._active = false;
                this.PluginOptions?.loading?.hide();
            }

            return originalPlayTrailers(item);
        };
    }

    async playTrailersInApp(item) {
        const playbackManager = this.PluginOptions?.playbackManager;
        if (!item || !playbackManager) {
            return false;
        }

        const apiClient = this.PluginOptions.ServerConnections.getApiClient(item.ServerId);
        let trailers = [];

        if (item.LocalTrailerCount) {
            try {
                trailers = await apiClient.getLocalTrailers(apiClient.getCurrentUserId(), item.Id) || [];
            } catch (error) {
                console.warn('Failed to load local trailers', error);
            }
        }

        if (!trailers.length && item.RemoteTrailers?.length) {
            trailers = item.RemoteTrailers.map((trailer) => ({
                Name: trailer.Name || (item.Name + ' Trailer'),
                Url: trailer.Url,
                MediaType: 'Video',
                Type: 'Trailer',
                ServerId: apiClient.serverId()
            }));
        }

        if (!trailers.length) {
            return false;
        }

        const trailer = trailers[0];
        const url = trailer.Url || trailer.Path;
        const trailerType = getTrailerType(url);

        if (trailer.Id && !trailerType) {
            await playbackManager.play({ items: [trailer], fullscreen: true });
            return true;
        }

        if (!trailerType) {
            return false;
        }

        this._active = true;
        await playbackManager.play({ items: [trailer], fullscreen: true });
        return true;
    }

    canPlayMediaType(mediaType) {
        return (mediaType || '').toLowerCase() === 'video';
    }

    canPlayItem() {
        return false;
    }

    canPlayUrl(url) {
        return this._active && !!getTrailerType(url);
    }

    play(options) {
        const url = options?.url;
        const trailerType = getTrailerType(url);
        if (!trailerType) {
            return Promise.reject('ErrorDefault');
        }

        this._currentSrc = url;
        this._started = false;

        switch (trailerType) {
            case 'youtube':
                return this.playYoutube(url, options);
            case 'vimeo':
                return this.playVimeo(url, options);
            default:
                return this.playDirectVideo(url, options);
        }
    }

    playYoutube(url, options) {
        const videoId = getYoutubeVideoId(url);
        if (!videoId) {
            this.endPlayback();
            return Promise.reject('ErrorDefault');
        }

        const head = document.head || document.documentElement;
        if (head && !document.querySelector('meta[name="referrer"]')) {
            const meta = document.createElement('meta');
            meta.name = 'referrer';
            meta.content = 'strict-origin-when-cross-origin';
            head.appendChild(meta);
        }

        return loadYoutubeIframeApi().then(() => new Promise((resolve, reject) => {
            const fail = (reason) => {
                this.endPlayback();
                reject(reason || 'ErrorDefault');
            };

            try {
                const container = this.createFullscreenContainer();
                const hostId = 'uwp-trailer-yt-' + Date.now();
                container.innerHTML = `<div id="${hostId}" style="width:100%;height:100%;"></div>`;

                this._ytPlayer = new YT.Player(hostId, {
                    width: '100%',
                    height: '100%',
                    videoId: videoId,
                    host: 'https://www.youtube.com',
                    playerVars: {
                        autoplay: 1,
                        controls: 0,
                        enablejsapi: 1,
                        modestbranding: 1,
                        rel: 0,
                        fs: 0,
                        playsinline: 1,
                        origin: window.location.origin
                    },
                    events: {
                        onReady: (event) => event.target.playVideo(),
                        onStateChange: (event) => this.onYoutubeStateChange(event, options, resolve),
                        onError: (event) => {
                            console.error('YouTube trailer playback failed', event?.data);
                            fail('ErrorDefault');
                        }
                    }
                });
            } catch (error) {
                console.error('Failed to start YouTube trailer playback', error);
                fail('ErrorDefault');
            }
        }));
    }

    playVimeo(url, options) {
        const videoId = getVimeoVideoId(url);
        if (!videoId) {
            this.endPlayback();
            return Promise.reject('ErrorDefault');
        }

        return new Promise((resolve, reject) => {
            try {
                const container = this.createFullscreenContainer();
                const iframe = document.createElement('iframe');
                iframe.src = `https://player.vimeo.com/video/${videoId}?autoplay=1`;
                iframe.allow = 'autoplay; fullscreen; encrypted-media; picture-in-picture';
                iframe.referrerPolicy = 'strict-origin-when-cross-origin';
                iframe.setAttribute('allowfullscreen', '');
                iframe.style.cssText = 'width:100%;height:100%;border:0;background:#000;';
                container.appendChild(iframe);
                this.onPlaybackStarted(options);
                resolve();
            } catch (error) {
                console.error('Failed to start Vimeo trailer playback', error);
                this.endPlayback();
                reject('ErrorDefault');
            }
        });
    }

    playDirectVideo(url, options) {
        return new Promise((resolve, reject) => {
            try {
                const container = this.createFullscreenContainer();
                const video = document.createElement('video');
                video.src = url;
                video.autoplay = true;
                video.playsInline = true;
                video.controls = true;
                video.style.cssText = 'width:100%;height:100%;object-fit:contain;background:#000;';
                video.addEventListener('playing', () => {
                    if (!this._started) {
                        this.onPlaybackStarted(options);
                        resolve();
                    }
                }, { once: true });
                video.addEventListener('error', () => {
                    this.endPlayback();
                    reject('ErrorDefault');
                }, { once: true });
                video.addEventListener('ended', () => this.endPlayback());
                container.appendChild(video);
                this._mediaElement = video;
                video.play()?.catch(() => {
                    this.endPlayback();
                    reject('ErrorDefault');
                });
            } catch (error) {
                console.error('Failed to start direct trailer playback', error);
                this.endPlayback();
                reject('ErrorDefault');
            }
        });
    }

    createFullscreenContainer() {
        const container = document.createElement('div');
        container.classList.add('youtubePlayerContainer', 'onTop');
        // High z-index only while onTop; a permanent value blocks the video OSD on Xbox.
        container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:#000;z-index:1000;';
        document.body.insertBefore(container, document.body.firstChild);
        document.body.classList.add('hide-scroll');
        this._container = container;
        return container;
    }

    onYoutubeStateChange(event, options, resolve) {
        const events = this.PluginOptions?.events;
        switch (event.data) {
            case YT.PlayerState.PLAYING:
                if (!this._started) {
                    this.onPlaybackStarted(options);
                    resolve();
                } else {
                    events?.trigger(this, 'unpause');
                }
                this.startTimeUpdateInterval();
                break;
            case YT.PlayerState.PAUSED:
                events?.trigger(this, 'pause');
                break;
            case YT.PlayerState.ENDED:
                this.endPlayback();
                break;
        }
    }

    releaseOsdFocus() {
        if (!this._container) {
            return;
        }

        this._container.classList.remove('onTop');
        this._container.style.zIndex = '';
        this._container.style.pointerEvents = 'none';
    }

    startTimeUpdateInterval() {
        this.clearTimeUpdateInterval();
        const events = this.PluginOptions?.events;
        if (!events) {
            return;
        }

        this._timeUpdateInterval = setInterval(() => events.trigger(this, 'timeupdate'), 500);
    }

    clearTimeUpdateInterval() {
        if (this._timeUpdateInterval) {
            clearInterval(this._timeUpdateInterval);
            this._timeUpdateInterval = null;
        }
    }

    onPlaybackStarted(options) {
        this._started = true;
        this.PluginOptions?.loading?.hide();

        if (options?.fullscreen !== false && this.PluginOptions?.appRouter?.showVideoOsd) {
            this.PluginOptions.appRouter.showVideoOsd().then(() => {
                this.releaseOsdFocus();
            }).catch(() => { });
        } else {
            this.releaseOsdFocus();
        }

        this.PluginOptions?.events?.trigger(this, 'playing');
    }

    endPlayback() {
        const src = this._currentSrc;
        const events = this.PluginOptions?.events;

        this.clearTimeUpdateInterval();

        if (this._ytPlayer) {
            try {
                this._ytPlayer.destroy();
            } catch (error) {
                console.warn('Failed to destroy YouTube trailer player', error);
            }
            this._ytPlayer = null;
        }

        if (this._mediaElement) {
            this._mediaElement.pause();
            this._mediaElement = null;
        }

        this._container?.remove();
        this._container = null;
        document.body.classList.remove('hide-scroll');

        this._currentSrc = null;
        this._started = false;
        this._active = false;

        if (events && src) {
            events.trigger(this, 'stopped', [{ src: src }]);
        }
    }

    stop() {
        if (this._currentSrc) {
            this.endPlayback();
        } else {
            this._active = false;
        }

        return Promise.resolve();
    }

    destroy() {
        this.stop();
    }

    getDeviceProfile() {
        return Promise.resolve({});
    }

    currentSrc() {
        return this._currentSrc;
    }

    currentTime(val) {
        if (this._ytPlayer) {
            if (val != null) {
                this._ytPlayer.seekTo(val / 1000, true);
                return;
            }

            return this._ytPlayer.getCurrentTime() * 1000;
        }

        if (this._mediaElement) {
            if (val != null) {
                this._mediaElement.currentTime = val / 1000;
                return;
            }

            return this._mediaElement.currentTime * 1000;
        }

        return null;
    }

    duration() {
        if (this._ytPlayer) {
            const duration = this._ytPlayer.getDuration();
            return duration > 0 ? duration * 1000 : null;
        }

        const duration = this._mediaElement?.duration;
        return duration > 0 && duration !== Infinity ? duration * 1000 : null;
    }

    pause() {
        this._ytPlayer?.pauseVideo();
        this._mediaElement?.pause();
    }

    unpause() {
        this._ytPlayer?.playVideo();
        this._mediaElement?.play();
    }

    paused() {
        if (this._ytPlayer) {
            return this._ytPlayer.getPlayerState() === YT.PlayerState.PAUSED;
        }

        return this._mediaElement ? this._mediaElement.paused : !this._started;
    }

    volume(val) {
        if (val != null) {
            this._ytPlayer?.setVolume(val);
            return;
        }

        return this._ytPlayer?.getVolume() ?? 100;
    }

    setVolume(val) {
        this.volume(val);
    }

    getVolume() {
        return this.volume();
    }

    setMute(mute) {
        if (!this._ytPlayer) {
            return;
        }

        if (mute) {
            this._ytPlayer.mute();
        } else {
            this._ytPlayer.unMute();
        }
    }

    isMuted() {
        if (this._ytPlayer) {
            return this._ytPlayer.isMuted();
        }

        return false;
    }
}

function getTrailerType(url) {
    if (!url || typeof url !== 'string' || !/^https?:\/\//i.test(url)) {
        return null;
    }

    const lower = url.toLowerCase();
    if (lower.includes('youtube.com') || lower.includes('youtu.be') || lower.includes('youtube-nocookie.com')) {
        return 'youtube';
    }

    if (lower.includes('vimeo.com')) {
        return 'vimeo';
    }

    if (/\.(mp4|webm|mkv|mov|m4v|avi|mpg|mpeg)(\?|#|$)/i.test(url)
        || /\/(videos|stream|trailer)/i.test(url)) {
        return 'direct';
    }

    return null;
}

function loadYoutubeIframeApi() {
    return new Promise((resolve) => {
        if (window.YT?.Player) {
            resolve();
            return;
        }

        const previousReady = window.onYouTubeIframeAPIReady;
        window.onYouTubeIframeAPIReady = function () {
            previousReady?.();
            resolve();
        };

        if (!document.querySelector('script[src*="youtube.com/iframe_api"]')) {
            const tag = document.createElement('script');
            tag.src = 'https://www.youtube.com/iframe_api';
            (document.head || document.documentElement).appendChild(tag);
        }
    });
}

function getYoutubeVideoId(url) {
    try {
        const parsed = new URL(url);
        if (parsed.hostname.includes('youtu.be')) {
            return parsed.pathname.replace(/^\//, '').split('/')[0] || null;
        }

        return parsed.searchParams.get('v')
            || parsed.pathname.match(/\/embed\/([^/?]+)/i)?.[1]
            || null;
    } catch (error) {
        console.warn('Failed to parse YouTube trailer URL', error);
        return null;
    }
}

function getVimeoVideoId(url) {
    const match = url.match(/vimeo\.com\/(?:channels\/[^/]+\/|groups\/[^/]+\/videos\/|video\/)?(\d+)/i);
    return match ? match[1] : null;
}

window["UwpTrailerPlayer"] = async () => UwpTrailerPlayer;

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
