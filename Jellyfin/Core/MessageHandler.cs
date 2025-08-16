using System.Diagnostics;
using System.Threading.Tasks;
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
public class MessageHandler
{
    private readonly Frame _frame;
    private readonly FullScreenManager _fullScreenManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandler"/> class.
    /// </summary>
    /// <param name="frame">Frame.</param>
    public MessageHandler(Frame frame)
    {
        _frame = frame;
        _fullScreenManager = new FullScreenManager();
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
            await _fullScreenManager.EnableFullscreenAsync(args).ConfigureAwait(true);
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
        else
        {
            Debug.WriteLine($"Unexpected JSON message: {eventType}");
        }
    }

    private void Exit()
    {
        Application.Current.Exit();
    }
}
