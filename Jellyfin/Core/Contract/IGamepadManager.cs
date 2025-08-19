using System;
using Windows.UI.Core;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Handles gamepad input and provices an interface for managing gamepad-related actions.
/// </summary>
public interface IGamepadManager
{
    /// <summary>
    /// Registers a handler for the back button event on the system navigation manager.
    /// </summary>
    /// <param name="handler">Callback delegate for handling the back action.</param>
    /// <param name="priority">Priority with which the handler should be executed.</param>
    /// <returns>A disposable that can be used to revoke the registration.</returns>
    IDisposable ObserveBackEvent(Action<BackRequestedEventArgs> handler, int priority = int.MaxValue);
}
