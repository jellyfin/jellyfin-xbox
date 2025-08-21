using System.Threading.Tasks;
using Windows.Data.Json;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Defines methods for managing full screen mode in the application.
/// </summary>
public interface IFullScreenManager
{
    /// <summary>
    /// Enables Fullscreen.
    /// </summary>
    /// <param name="args">JsonObject.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    Task EnableFullscreenAsync(JsonObject args);

    /// <summary>
    /// Disables FullScreen.
    /// </summary>
    /// <returns>A task that completes when the fullscreen has been closed.</returns>
    Task DisableFullScreen();
}
