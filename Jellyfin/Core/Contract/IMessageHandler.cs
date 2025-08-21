using System.Threading.Tasks;
using Windows.Data.Json;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Defines methods for handling messages from winuwp.js.
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Handle Json Notification from winuwp.js.
    /// </summary>
    /// <param name="json">JsonObject.</param>
    /// <returns>A task that completes when the notification action has been performed.</returns>
    Task HandleJsonNotification(JsonObject json);
}
