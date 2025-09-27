using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Jellyfin.Core.Contract;
using Jellyfin.Views;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Jellyfin.Core;

/// <summary>
/// Handle Json Notification from winuwp.js.
/// </summary>
public class MessageHandler : IMessageHandler
{
    private readonly Frame _frame;
    private readonly IFullScreenManager _fullScreenManager;
    private readonly IMessenger _messenger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandler"/> class.
    /// </summary>
    /// <param name="frame">Frame.</param>
    /// <param name="fullScreenManager">The service responsible for managing HDMI and fullscreen states.</param>
    /// <param name="messenger">The Messenger service.</param>
    public MessageHandler(Frame frame, IFullScreenManager fullScreenManager, IMessenger messenger)
    {
        _frame = frame;
        _fullScreenManager = fullScreenManager;
        _messenger = messenger;
    }

    /// <summary>
    /// Handle Json Notification from winuwp.js.
    /// </summary>
    /// <param name="json">JsonObject.</param>
    /// <returns>A task that completes when the notification action has been performed.</returns>
    public async Task HandleJsonNotification(JsonObject json)
    {
        var eventType = json.GetNamedString("type");
        var args = json.GetNamedObject("args");

        if (eventType == "enableFullscreen")
        {
            Central.Settings.JellyfinServer = null;
            _ = _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await _fullScreenManager.EnableFullscreenAsync(args).ConfigureAwait(true);
            });
        }
        else if (eventType == "disableFullscreen")
        {
            await _fullScreenManager.DisableFullScreen().ConfigureAwait(true);
        }
        else if (eventType == "selectServer")
        {
            Central.Settings.JellyfinServer = null;
            _ = _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _frame.Navigate(typeof(OnBoarding));
            });
        }
        else if (eventType == "openClientSettings")
        {
            _ = _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var settingsPopup = new Popup()
                {
                    HorizontalOffset = 0,
                    VerticalOffset = 0,
                    Width = Window.Current.Bounds.Width,
                    Height = Window.Current.Bounds.Height,
                };
                settingsPopup.Child = new Settings()
                {
                    ParentPopup = settingsPopup,
                    Width = Window.Current.Bounds.Width,
                    Height = Window.Current.Bounds.Height,
                };
                settingsPopup.IsOpen = true;
                (settingsPopup.Child as Control).Focus(FocusState.Programmatic);
            });
        }
        else if (eventType == "exit")
        {
            Exit();
        }
        else if (eventType == "loaded")
        {
            _messenger.Send(new WebMessage(eventType, args));
        }
        else
        {
            Debug.WriteLine($"Unexpected JSON message: {eventType}");
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private void Exit()
    {
        Application.Current.Exit();
    }
}
