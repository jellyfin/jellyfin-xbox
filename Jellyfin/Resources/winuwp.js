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
            return ["UwpXboxHdmiSetupPlugin"];
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
