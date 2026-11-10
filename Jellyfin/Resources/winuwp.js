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
            // Remote trailers and external URL streams have no library media source to probe.
            if (item.Url || item.Type === 'Trailer' || (typeof getTrailerType === 'function' && getTrailerType(item.Url || item.Path))) {
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
 * Hardened in-app fullscreen trailer playback for YouTube, Vimeo, and direct video URLs.
 * Eliminates all PR #178 audit findings (CRIT-2, CRIT-3, CRIT-4, MAJ-5 through MAJ-13, MIN-2 through MIN-5).
 */
let ytApiPromise = null;

function loadYoutubeIframeApi() {
    if (window.YT?.Player) {
        return Promise.resolve();
    }
    if (ytApiPromise) {
        return ytApiPromise;
    }

    ytApiPromise = new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            ytApiPromise = null;
            reject(new Error('YouTube IFrame API load timed out'));
        }, 15000);

        const onReady = () => {
            clearTimeout(timeout);
            resolve();
        };

        const existingReady = window.onYouTubeIframeAPIReady;
        window.onYouTubeIframeAPIReady = function () {
            try {
                existingReady?.();
            } finally {
                onReady();
            }
        };

        let tag = document.querySelector('script[src*="youtube.com/iframe_api"]');
        if (tag) {
            try { tag.remove(); } catch (_) { }
        }
        tag = document.createElement('script');
        tag.src = 'https://www.youtube.com/iframe_api';
        tag.async = true;
        tag.onerror = (err) => {
            clearTimeout(timeout);
            ytApiPromise = null;
            try { tag.remove(); } catch (_) { }
            reject(err || new Error('Failed to load YouTube IFrame API'));
        };
        (document.head || document.documentElement).appendChild(tag);
    }).catch((err) => {
        ytApiPromise = null;
        throw err;
    });

    return ytApiPromise;
}

function getYoutubeVideoId(url) {
    if (!url || typeof url !== 'string') {
        return null;
    }

    url = url.trim();

    try {
        const parsed = new URL(url);
        const host = parsed.hostname.toLowerCase();

        if (host.includes('youtu.be')) {
            const id = parsed.pathname.replace(/^\/+/, '').split('/')[0].split('?')[0];
            return /^[a-zA-Z0-9_-]{11}$/.test(id) ? id : null;
        }

        if (host.includes('youtube.com') || host.includes('youtube-nocookie.com')) {
            const vParam = parsed.searchParams.get('v');
            if (vParam && /^[a-zA-Z0-9_-]{11}$/.test(vParam)) {
                return vParam;
            }

            const pathMatches = parsed.pathname.match(/\/(?:embed|shorts|live|v|watch)\/([a-zA-Z0-9_-]{11})/i);
            if (pathMatches) {
                return pathMatches[1];
            }
        }
    } catch (_) {
        // Fallback to regex if URL parsing fails
    }

    const match = url.match(/(?:youtu\.be\/|youtube(?:-nocookie)?\.com\/(?:embed\/|shorts\/|live\/|v\/|watch\/|watch\?(?:.*&)?v=))([a-zA-Z0-9_-]{11})/i);
    return match ? match[1] : null;
}

function getVimeoVideoInfo(url) {
    if (!url || typeof url !== 'string') {
        return null;
    }

    url = url.trim();

    const idMatch = url.match(/vimeo\.com\/(?:channels\/[^/]+\/|groups\/[^/]+\/videos\/|video\/|manage\/videos\/)?(\d+)/i);
    if (!idMatch) {
        return null;
    }

    const id = idMatch[1];
    let hash = null;
    try {
        const parsed = new URL(url);
        hash = parsed.searchParams.get('h');
        if (!hash) {
            const pathParts = parsed.pathname.split('/').filter(Boolean);
            const idIdx = pathParts.indexOf(id);
            if (idIdx !== -1 && pathParts[idIdx + 1] && /^[a-zA-Z0-9_-]+$/.test(pathParts[idIdx + 1])) {
                hash = pathParts[idIdx + 1];
            }
        }
    } catch (_) { }

    return { id, hash };
}

function getTrailerType(url) {
    if (!url || typeof url !== 'string' || !/^https?:\/\//i.test(url.trim())) {
        return null;
    }

    const lower = url.trim().toLowerCase();
    if (lower.includes('youtube.com') || lower.includes('youtu.be') || lower.includes('youtube-nocookie.com')) {
        return 'youtube';
    }

    if (lower.includes('vimeo.com')) {
        return 'vimeo';
    }

    if (/\.(mp4|webm|mkv|mov|m4v|avi|mpg|mpeg)(\?|#|$)/i.test(lower)
        || /\/(videos|stream|trailer)/i.test(lower)) {
        return 'direct';
    }

    return null;
}

class UwpTrailerPlayer {
    constructor(pluginOptions) {
        this.name = 'UWP Trailer Player';
        this.type = 'mediaplayer';
        this.id = 'uwptrailerplayer';
        this.priority = 10;
        this.isLocalPlayer = true;
        this.PluginOptions = pluginOptions;
        this._currentSrc = null;
        this._started = false;
        this._active = false;
        this._container = null;
        this._mediaElement = null;
        this._ytPlayer = null;
        this._ytReady = false;
        this._timeUpdateInterval = null;
        this._vimeoMessageListener = null;
        this._injectedMetaReferrer = null;
        this._playbackSessionId = 0;
        this._vimeoIframe = null;
        this._vimeoReady = false;
        this._vimeoCurrentTime = null;
        this._vimeoDuration = null;
        this._vimeoPaused = false;
        this._vimeoVolume = 100;
        this._vimeoMuted = false;

        this.patchPlayTrailers();
    }

    patchPlayTrailers() {
        const playbackManager = this.PluginOptions?.playbackManager;
        if (!playbackManager || playbackManager._uwpTrailerPatchApplied) {
            return;
        }

        playbackManager._uwpTrailerPatchApplied = true;
        const originalPlayTrailers = typeof playbackManager.playTrailers === 'function'
            ? playbackManager.playTrailers.bind(playbackManager)
            : null;

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

            if (originalPlayTrailers) {
                return originalPlayTrailers(item);
            }
        };
    }

    async playTrailersInApp(item) {
        const playbackManager = this.PluginOptions?.playbackManager;
        if (!item || !playbackManager) {
            return false;
        }

        const serverConns = this.PluginOptions?.ServerConnections;
        const apiClient = (serverConns?.getApiClient && item.ServerId ? serverConns.getApiClient(item.ServerId) : null)
            || (serverConns?.currentApiClient ? serverConns.currentApiClient() : null);

        let trailers = [];

        if (item.LocalTrailerCount && apiClient) {
            try {
                trailers = await apiClient.getLocalTrailers(apiClient.getCurrentUserId(), item.Id) || [];
            } catch (error) {
                console.warn('Failed to load local trailers', error);
            }
        }

        if (!trailers.length && item.RemoteTrailers?.length) {
            const sId = apiClient?.serverId ? apiClient.serverId() : (item.ServerId || '');
            trailers = item.RemoteTrailers.map((trailer) => ({
                Name: trailer.Name || (item.Name + ' Trailer'),
                Url: trailer.Url,
                MediaType: 'Video',
                Type: 'Trailer',
                ServerId: sId
            }));
        }

        if (!trailers.length) {
            return false;
        }

        const trailer = trailers.find(t => t.Id || !!getTrailerType(t.Url || t.Path)) || trailers[0];
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
        try {
            await playbackManager.play({ items: [trailer], url: url, fullscreen: true });
            return true;
        } catch (err) {
            this._active = false;
            throw err;
        }
    }

    canPlayMediaType(mediaType) {
        return (mediaType || '').toLowerCase() === 'video';
    }

    canPlayItem(item) {
        if (!this._active || !item) {
            return false;
        }
        const url = item.Url || item.Path;
        return !!getTrailerType(url);
    }

    canPlayUrl(url) {
        return this._active && !!getTrailerType(url);
    }

    play(options) {
        this.endPlayback();

        const url = options?.url || options?.item?.Url || options?.item?.Path || options?.items?.[0]?.Url || options?.items?.[0]?.Path;
        const trailerType = getTrailerType(url);
        if (!trailerType) {
            this._active = false;
            return Promise.reject('ErrorDefault');
        }

        this._active = true;
        this._currentSrc = url;
        this._started = false;
        const sessionId = ++this._playbackSessionId;

        switch (trailerType) {
            case 'youtube':
                return this.playYoutube(url, options, sessionId);
            case 'vimeo':
                return this.playVimeo(url, options, sessionId);
            default:
                return this.playDirectVideo(url, options, sessionId);
        }
    }

    playYoutube(url, options, sessionId) {
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
            this._injectedMetaReferrer = meta;
        }

        return loadYoutubeIframeApi().then(() => new Promise((resolve, reject) => {
            if (this._playbackSessionId !== sessionId || !this._active || this._currentSrc !== url) {
                reject('PlaybackCancelled');
                return;
            }

            const fail = (reason) => {
                this.endPlayback();
                reject(reason || 'ErrorDefault');
            };

            try {
                const container = this.createFullscreenContainer();
                const hostId = 'uwp-trailer-yt-' + Date.now();
                container.innerHTML = `<div id="${hostId}" style="width:100%;height:100%;" tabindex="-1" aria-hidden="true"></div>`;

                const playerVars = {
                    autoplay: 1,
                    controls: 0,
                    enablejsapi: 1,
                    modestbranding: 1,
                    rel: 0,
                    fs: 0,
                    playsinline: 1
                };

                const origin = window.location.origin;
                if (origin && /^https?:\/\//i.test(origin)) {
                    playerVars.origin = origin;
                }

                this._ytPlayer = new YT.Player(hostId, {
                    width: '100%',
                    height: '100%',
                    videoId: videoId,
                    host: 'https://www.youtube.com',
                    playerVars: playerVars,
                    events: {
                        onReady: (event) => {
                            if (this._playbackSessionId !== sessionId || !this._active) {
                                try { event.target.destroy(); } catch (_) { }
                                return;
                            }
                            this._ytReady = true;
                            const iframe = event.target.getIframe?.();
                            if (iframe) {
                                iframe.setAttribute('tabindex', '-1');
                                iframe.setAttribute('aria-hidden', 'true');
                                iframe.setAttribute('inert', '');
                            }
                            event.target.playVideo();
                        },
                        onStateChange: (event) => {
                            if (this._playbackSessionId !== sessionId || !this._active) {
                                return;
                            }
                            this.onYoutubeStateChange(event, options, resolve);
                        },
                        onError: (event) => {
                            console.error('YouTube trailer playback error code:', event?.data);
                            fail('ErrorDefault');
                        }
                    }
                });
            } catch (error) {
                console.error('Failed to instantiate YouTube player', error);
                fail('ErrorDefault');
            }
        }));
    }

    playVimeo(url, options, sessionId) {
        const info = getVimeoVideoInfo(url);
        if (!info?.id) {
            this.endPlayback();
            return Promise.reject('ErrorDefault');
        }

        if (sessionId === undefined) {
            sessionId = ++this._playbackSessionId;
        }
        this._active = true;
        this._currentSrc = url;

        return new Promise((resolve, reject) => {
            if (this._playbackSessionId !== sessionId || !this._active) {
                reject('PlaybackCancelled');
                return;
            }

            try {
                const container = this.createFullscreenContainer();
                const iframe = document.createElement('iframe');
                let src = `https://player.vimeo.com/video/${info.id}?autoplay=1&api=1`;
                if (info.hash) {
                    src += `&h=${info.hash}`;
                }
                iframe.src = src;
                iframe.allow = 'autoplay; fullscreen; encrypted-media; picture-in-picture';
                iframe.referrerPolicy = 'strict-origin-when-cross-origin';
                iframe.setAttribute('allowfullscreen', '');
                iframe.setAttribute('tabindex', '-1');
                iframe.setAttribute('aria-hidden', 'true');
                iframe.setAttribute('inert', '');
                iframe.style.cssText = 'width:100%;height:100%;border:0;background:#000;';
                container.appendChild(iframe);
                this._vimeoIframe = iframe;

                const messageHandler = (e) => {
                    if (this._playbackSessionId !== sessionId || !this._active) return;
                    if (!e.data || typeof e.data !== 'string') return;
                    try {
                        const data = JSON.parse(e.data);
                        if (data.event === 'ready') {
                            this._vimeoReady = true;
                            iframe.contentWindow?.postMessage(JSON.stringify({ method: 'addEventListener', value: 'finish' }), '*');
                            iframe.contentWindow?.postMessage(JSON.stringify({ method: 'addEventListener', value: 'ended' }), '*');
                            iframe.contentWindow?.postMessage(JSON.stringify({ method: 'addEventListener', value: 'play' }), '*');
                            iframe.contentWindow?.postMessage(JSON.stringify({ method: 'addEventListener', value: 'pause' }), '*');
                            iframe.contentWindow?.postMessage(JSON.stringify({ method: 'addEventListener', value: 'timeupdate' }), '*');
                        } else if (data.event === 'finish' || data.event === 'ended') {
                            this.endPlayback();
                        } else if (data.event === 'pause') {
                            this._vimeoPaused = true;
                            this.PluginOptions?.events?.trigger(this, 'pause');
                        } else if (data.event === 'play') {
                            this._vimeoPaused = false;
                            this.PluginOptions?.events?.trigger(this, 'unpause');
                        } else if (data.event === 'timeupdate') {
                            if (data.data?.seconds != null) {
                                this._vimeoCurrentTime = data.data.seconds * 1000;
                            }
                            if (data.data?.duration != null) {
                                this._vimeoDuration = data.data.duration * 1000;
                            }
                            this.PluginOptions?.events?.trigger(this, 'timeupdate');
                        }
                    } catch (_) { }
                };

                window.addEventListener('message', messageHandler);
                this._vimeoMessageListener = messageHandler;

                this.onPlaybackStarted(options);
                resolve();
            } catch (error) {
                console.error('Failed to start Vimeo trailer playback', error);
                this.endPlayback();
                reject('ErrorDefault');
            }
        });
    }

    playDirectVideo(url, options, sessionId) {
        if (sessionId === undefined) {
            sessionId = ++this._playbackSessionId;
        }
        this._active = true;
        this._currentSrc = url;

        return new Promise((resolve, reject) => {
            if (this._playbackSessionId !== sessionId || !this._active) {
                reject('PlaybackCancelled');
                return;
            }

            try {
                const container = this.createFullscreenContainer();
                const video = document.createElement('video');
                video.src = url;
                video.autoplay = true;
                video.playsInline = true;
                video.controls = false;
                video.setAttribute('tabindex', '-1');
                video.setAttribute('aria-hidden', 'true');
                video.setAttribute('inert', '');
                video.style.cssText = 'width:100%;height:100%;object-fit:contain;background:#000;';

                const events = this.PluginOptions?.events;

                video.addEventListener('playing', () => {
                    if (this._playbackSessionId !== sessionId || !this._active || this._mediaElement !== video) {
                        return;
                    }
                    if (!this._started) {
                        this.onPlaybackStarted(options);
                        this.startTimeUpdateInterval();
                        resolve();
                    } else {
                        events?.trigger(this, 'unpause');
                    }
                });

                video.addEventListener('pause', () => {
                    if (this._playbackSessionId !== sessionId || !this._active || this._mediaElement !== video) {
                        return;
                    }
                    events?.trigger(this, 'pause');
                });

                video.addEventListener('error', () => {
                    if (this._playbackSessionId !== sessionId || !this._active || this._mediaElement !== video) {
                        return;
                    }
                    this.endPlayback();
                    reject('ErrorDefault');
                }, { once: true });

                video.addEventListener('ended', () => {
                    if (this._playbackSessionId !== sessionId || !this._active || this._mediaElement !== video) {
                        return;
                    }
                    this.endPlayback();
                }, { once: true });

                container.appendChild(video);
                this._mediaElement = video;

                video.play()?.catch(() => {
                    if (this._playbackSessionId !== sessionId || !this._active || this._mediaElement !== video) {
                        return;
                    }
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
        if (this._container) {
            this._container.remove();
        }

        const container = document.createElement('div');
        container.classList.add('youtubePlayerContainer', 'onTop');
        container.setAttribute('tabindex', '-1');
        container.setAttribute('aria-hidden', 'true');
        container.setAttribute('inert', '');
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
                this.clearTimeUpdateInterval();
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
        this._container.tabIndex = -1;
        this._container.setAttribute('inert', '');
        this._container.setAttribute('aria-hidden', 'true');

        const osdElement = document.querySelector('.videoOsd, .videoPlayerContainer, [data-role="page"]');
        if (osdElement && typeof osdElement.focus === 'function') {
            osdElement.focus();
        }
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

        try {
            window.chrome?.webview?.postMessage(JSON.stringify({ type: 'enableFullscreen', args: {} }));
        } catch (_) { }

        if (options?.fullscreen !== false && this.PluginOptions?.appRouter?.showVideoOsd) {
            Promise.resolve(this.PluginOptions.appRouter.showVideoOsd())
                .catch(() => { })
                .finally(() => this.releaseOsdFocus());
        } else {
            this.releaseOsdFocus();
        }

        this.PluginOptions?.events?.trigger(this, 'playing');
    }

    endPlayback() {
        this._playbackSessionId++;
        const src = this._currentSrc;
        const events = this.PluginOptions?.events;

        try {
            window.chrome?.webview?.postMessage(JSON.stringify({ type: 'disableFullscreen', args: {} }));
        } catch (_) { }

        this.clearTimeUpdateInterval();

        if (this._vimeoMessageListener) {
            window.removeEventListener('message', this._vimeoMessageListener);
            this._vimeoMessageListener = null;
        }
        this._vimeoIframe = null;
        this._vimeoReady = false;
        this._vimeoCurrentTime = null;
        this._vimeoDuration = null;
        this._vimeoPaused = false;
        this._vimeoVolume = 100;
        this._vimeoMuted = false;

        if (this._ytPlayer) {
            try {
                this._ytPlayer.destroy();
            } catch (error) {
                console.warn('Failed to destroy YouTube trailer player', error);
            }
            this._ytPlayer = null;
            this._ytReady = false;
        }

        if (this._mediaElement) {
            try {
                this._mediaElement.pause();
                this._mediaElement.removeAttribute('src');
                this._mediaElement.load();
            } catch (_) { }
            this._mediaElement = null;
        }

        if (this._injectedMetaReferrer) {
            this._injectedMetaReferrer.remove();
            this._injectedMetaReferrer = null;
        }

        if (this._container) {
            this._container.remove();
            this._container = null;
        }

        document.body.classList.remove('hide-scroll');

        this._currentSrc = null;
        this._started = false;
        this._active = false;

        if (events && src) {
            events.trigger(this, 'stopped', [{ src: src }]);
        }
    }

    stop() {
        this.endPlayback();
        return Promise.resolve();
    }

    destroy() {
        return this.stop();
    }

    getDeviceProfile() {
        return Promise.resolve({});
    }

    currentSrc() {
        return this._currentSrc;
    }

    currentTime(val) {
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.getCurrentTime === 'function') {
            try {
                if (val != null) {
                    this._ytPlayer.seekTo(val / 1000, true);
                    return;
                }
                return this._ytPlayer.getCurrentTime() * 1000;
            } catch (_) {
                return null;
            }
        }

        if (this._vimeoIframe) {
            try {
                if (val != null) {
                    this._vimeoIframe.contentWindow?.postMessage(JSON.stringify({ method: 'setCurrentTime', value: val / 1000 }), '*');
                    this._vimeoCurrentTime = val;
                    return;
                }
                return this._vimeoCurrentTime;
            } catch (_) {
                return null;
            }
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
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.getDuration === 'function') {
            try {
                const dur = this._ytPlayer.getDuration();
                return dur > 0 ? dur * 1000 : null;
            } catch (_) {
                return null;
            }
        }

        if (this._vimeoIframe) {
            return this._vimeoDuration ?? null;
        }

        const dur = this._mediaElement?.duration;
        return dur > 0 && dur !== Infinity ? dur * 1000 : null;
    }

    pause() {
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.pauseVideo === 'function') {
            try { this._ytPlayer.pauseVideo(); } catch (_) { }
        }
        if (this._vimeoIframe) {
            try {
                this._vimeoIframe.contentWindow?.postMessage(JSON.stringify({ method: 'pause' }), '*');
                this._vimeoPaused = true;
            } catch (_) { }
        }
        this._mediaElement?.pause();
    }

    unpause() {
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.playVideo === 'function') {
            try { this._ytPlayer.playVideo(); } catch (_) { }
        }
        if (this._vimeoIframe) {
            try {
                this._vimeoIframe.contentWindow?.postMessage(JSON.stringify({ method: 'play' }), '*');
                this._vimeoPaused = false;
            } catch (_) { }
        }
        this._mediaElement?.play()?.catch?.(() => { });
    }

    paused() {
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.getPlayerState === 'function') {
            try {
                return this._ytPlayer.getPlayerState() === YT.PlayerState.PAUSED;
            } catch (_) {
                return !this._started;
            }
        }

        if (this._vimeoIframe) {
            return this._vimeoPaused ?? !this._started;
        }

        return this._mediaElement ? this._mediaElement.paused : !this._started;
    }

    volume(val) {
        if (val != null) {
            if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.setVolume === 'function') {
                try { this._ytPlayer.setVolume(val); } catch (_) { }
            }
            if (this._vimeoIframe) {
                try {
                    this._vimeoVolume = val;
                    this._vimeoIframe.contentWindow?.postMessage(JSON.stringify({ method: 'setVolume', value: Math.max(0, Math.min(1, val / 100)) }), '*');
                } catch (_) { }
            }
            if (this._mediaElement) {
                this._mediaElement.volume = Math.max(0, Math.min(1, val / 100));
            }
            return;
        }

        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.getVolume === 'function') {
            try { return this._ytPlayer.getVolume(); } catch (_) { }
        }

        if (this._vimeoIframe) {
            return this._vimeoVolume ?? 100;
        }

        if (this._mediaElement) {
            return Math.round(this._mediaElement.volume * 100);
        }

        return 100;
    }

    setVolume(val) {
        this.volume(val);
    }

    getVolume() {
        return this.volume();
    }

    setMute(mute) {
        if (this._ytPlayer && this._ytReady) {
            try {
                if (mute) {
                    this._ytPlayer.mute();
                } else {
                    this._ytPlayer.unMute();
                }
            } catch (_) { }
        }

        if (this._vimeoIframe) {
            try {
                this._vimeoMuted = !!mute;
                this._vimeoIframe.contentWindow?.postMessage(JSON.stringify({ method: 'setMuted', value: !!mute }), '*');
            } catch (_) { }
        }

        if (this._mediaElement) {
            this._mediaElement.muted = !!mute;
        }
    }

    isMuted() {
        if (this._ytPlayer && this._ytReady && typeof this._ytPlayer.isMuted === 'function') {
            try { return this._ytPlayer.isMuted(); } catch (_) { }
        }

        if (this._vimeoIframe) {
            return this._vimeoMuted ?? false;
        }

        if (this._mediaElement) {
            return this._mediaElement.muted;
        }

        return false;
    }
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
