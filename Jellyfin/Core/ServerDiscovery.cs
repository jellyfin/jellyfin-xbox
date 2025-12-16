using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Core.Contract;
using Jellyfin.Models;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.System.Threading;
using Windows.UI.Xaml;

namespace Jellyfin.Core;

/// <summary>
/// Handles autodiscovery of Jellyfin Servers over the local network.
/// </summary>
public sealed class ServerDiscovery : IDisposable, IServerDiscovery
{
    private readonly byte[] _sendBuffer = Encoding.ASCII.GetBytes("Who is JellyfinServer?");
    private readonly UdpClient _udpSocket = new UdpClient(7359);
    private readonly ILogger<ServerDiscovery> _logger;
    private CancellationTokenSource _stopServerDiscovery = new CancellationTokenSource();
    private ThreadPoolTimer _discoveryTimer;
    private bool _disposed = false;
    private int _serverDiscoveryInvocations = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDiscovery"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ServerDiscovery(ILogger<ServerDiscovery> logger)
    {
        _udpSocket.EnableBroadcast = true;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Action<DiscoveredServer> OnDiscover;

    /// <inheritdoc/>
    public event Action OnServerDiscoveryEnded;

    /// <inheritdoc/>
    public void StartServerDiscovery()
    {
        ThrowIfDisposed();
        if (_discoveryTimer is not null)
        {
            _logger.LogInformation("Server discovery already in progress.");
            return;
        }

        _logger.LogInformation("Start server discovery");
        Task.Run(() =>
        {
            ReceiveDiscoveryMessages(_stopServerDiscovery.Token);
        });
        SendDiscoverMessage();

        _discoveryTimer = ThreadPoolTimer.CreatePeriodicTimer(
            (_) =>
            {
                SendDiscoverMessage();
                _serverDiscoveryInvocations++;
                if (_serverDiscoveryInvocations >= 2)
                {
                    StopServerDiscovery();
                }
            },
            TimeSpan.FromSeconds(10));
    }

    /// <inheritdoc/>
    public void StopServerDiscovery()
    {
        ThrowIfDisposed();
        _logger.LogInformation("Stopping server discovery");
        _stopServerDiscovery?.Cancel();
        _stopServerDiscovery?.Dispose();
        _stopServerDiscovery = new();
        _discoveryTimer?.Cancel();
        _discoveryTimer = null;
        _udpSocket?.Close();
        _serverDiscoveryInvocations = 0;
        OnServerDiscoveryEnded?.Invoke();
    }

    private void SendDiscoverMessage()
    {
        try
        {
            _logger.LogInformation("Sending discovery message");
            lock (_udpSocket)
            {
                _udpSocket.Send(_sendBuffer, _sendBuffer.Length, new IPEndPoint(IPAddress.Broadcast, 7359));
            }

            _logger.LogInformation("Server Discovery message send.");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, $"Broadcast errored with message: {ex.Message}");
        }
    }

    private void ReceiveDiscoveryMessages(CancellationToken cancellationToken)
    {
        var localEndPoint = new IPEndPoint(IPAddress.Any, 7359);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var dataReceived = _udpSocket.Receive(ref localEndPoint);
                if (dataReceived.Length == 0 || dataReceived.SequenceEqual(_sendBuffer))
                {
                    continue;
                }

                var text = Encoding.ASCII.GetString(dataReceived);
                _logger.LogDebug($"Server discovery Received: {text}");

                var discoveredServer = JsonSerializer.Deserialize<DiscoveredServer>(text);
                OnDiscover?.Invoke(discoveredServer);
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation requested, exit gracefully
                return;
            }
            catch (SocketException ex)
            {
                switch (ex.SocketErrorCode)
                {
                    case SocketError.TimedOut:
                        _logger.LogDebug(ex, $"Socket Timed out");
                        break;
                    case SocketError.Interrupted:
                        // Socket disposed
                        _logger.LogError(ex, "Socket interrupted.");
                        return;
                    default:
                        _logger.LogError(ex, $"Socket Error: {ex.Message}");
                        throw ex;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopServerDiscovery();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
