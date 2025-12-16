using System;
using Jellyfin.Models;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Provides methods for discovering Jellyfin servers on the local network.
/// </summary>
public interface IServerDiscovery : IDisposable
{
    /// <summary>
    /// Occurs when a new server is discovered on the network.
    /// </summary>
    /// <remarks>Subscribers are notified with a <see cref="DiscoveredServer"/> instance representing the
    /// discovered server. Event handlers are invoked on the thread that raises the event; ensure thread safety when
    /// accessing shared resources.</remarks>
    event Action<DiscoveredServer> OnDiscover;

    /// <summary>
    /// Occurs when the server discovery process has completed, regardless of whether any servers were found.
    /// </summary>
    /// <remarks>Subscribe to this event to be notified when server discovery finishes. This event is raised
    /// after all discovery attempts have concluded.</remarks>
    event Action OnServerDiscoveryEnded;

    /// <summary>
    /// Begins searching for available servers on the network.
    /// </summary>
    /// <remarks>Call this method to initiate the server discovery process. The method does not block and
    /// typically triggers asynchronous discovery events or callbacks. Ensure that server discovery is not already in
    /// progress before calling this method to avoid redundant operations.</remarks>
    void StartServerDiscovery();

    /// <summary>
    /// Stops the ongoing server discovery process.
    /// </summary>
    /// <remarks>Call this method to halt any active attempts to discover servers. If no discovery is in
    /// progress, this method has no effect.</remarks>
    void StopServerDiscovery();
}
