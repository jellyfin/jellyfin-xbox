using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Core;

namespace Jellyfin.Utils;

/// <summary>
/// Manages gamepad input, handles gamepad connection events, and raises events for specific button presses.
/// </summary>
public static class GamepadManager
{
    private static readonly IDictionary<SystemNavigationManager, List<(int Priority, Action<BackRequestedEventArgs> Execute)>> _gamepadActions;

    static GamepadManager()
    {
        _gamepadActions = new Dictionary<SystemNavigationManager, List<(int Priority, Action<BackRequestedEventArgs> Execute)>>();
    }

    /// <summary>
    /// Registers a handler for the back button event on the system navigation manager.
    /// </summary>
    /// <param name="handler">Callback delegate for handling the back action.</param>
    /// <param name="priority">Priority with which the handler should be executed.</param>
    /// <returns>A disposable that can be used to revoke the registration.</returns>
    public static IDisposable ObserveBackEvent(Action<BackRequestedEventArgs> handler, int priority = int.MaxValue)
    {
        var manager = SystemNavigationManager.GetForCurrentView();
        manager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        if (!_gamepadActions.TryGetValue(manager, out var queue))
        {
            _gamepadActions[manager] = queue = new List<(int, Action<BackRequestedEventArgs>)>();

            void OnManagerOnBackRequested(object sender, BackRequestedEventArgs args)
            {
                if (queue.Count > 0)
                {
                    foreach (var valueTuple in queue.OrderBy(e => e.Priority).TakeWhile(e => !args.Handled))
                    {
                        valueTuple.Execute?.Invoke(args);
                    }
                }
            }

            manager.BackRequested += OnManagerOnBackRequested;
        }

        queue.Add((priority, handler));

        return new UnregisterDisposable(handler, manager);
    }

    private static void UnregisterHandler(Action<BackRequestedEventArgs> handler, SystemNavigationManager systemNavigationManager)
    {
        if (_gamepadActions.TryGetValue(systemNavigationManager, out var queue))
        {
            queue.RemoveAll(e => e.Execute == handler);
        }
    }

    private sealed class UnregisterDisposable : IDisposable
    {
        private readonly Action<BackRequestedEventArgs> _handler;
        private readonly SystemNavigationManager _systemNavigationManager;

        public UnregisterDisposable(Action<BackRequestedEventArgs> handler, SystemNavigationManager systemNavigationManager)
        {
            _handler = handler;
            _systemNavigationManager = systemNavigationManager;
        }

        public void Dispose()
        {
            GamepadManager.UnregisterHandler(_handler, _systemNavigationManager);
        }
    }
}
