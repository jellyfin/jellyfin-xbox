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
                if (xboxSeries) {
                    options.maxVideoWidth = 3840;
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
            postMessage('enableFullscreen', videoInfo);
        },

        disableFullscreen: function () {
            postMessage('disableFullscreen');
        },

        getPlugins: function () {
            console.debug('getPlugins');
            return [];
        },

        selectServer: function () {
            postMessage('selectServer');
        },

        openClientSettings: function () {
            postMessage('openClientSettings');
        }
    };
})(APP_NAME, APP_VERSION, DEVICE_NAME, SUPPORTS_HDR, SUPPORTS_DOVI);
