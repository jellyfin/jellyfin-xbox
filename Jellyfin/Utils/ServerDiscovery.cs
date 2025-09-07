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
using Jellyfin.Models;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.System.Threading;
using Windows.UI.Xaml;

namespace Jellyfin.Utils
{
    /// <summary>
    /// Handles autodiscovery of Jellyfin Servers over the local network.
    /// </summary>
    public sealed class ServerDiscovery : IDisposable
    {
        private readonly ThreadPoolTimer _discoveryTimer;
        private readonly byte[] _sendBuffer = Encoding.ASCII.GetBytes("Who is JellyfinServer?");
        private readonly Socket _udpSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerDiscovery"/> class.
        /// </summary>
        public ServerDiscovery()
        {
            _udpSocket.EnableBroadcast = true;
            _udpSocket.ReceiveTimeout = 10000;

            _discoveryTimer = ThreadPoolTimer.CreatePeriodicTimer(
                (_) =>
                {
                    SendDiscoverMessage();
                },
                TimeSpan.FromSeconds(10));
            Task.Run(SendDiscoverMessage)
                .ContinueWith((_) => ReceiveDiscoveryMessages());
        }

        /// <summary>
        /// Triggered when a server has been discovered on the local network
        /// </summary>
        public event Action OnDiscover;

        /// <summary>
        /// Gets the queue of local servers that have not yet been retreived.
        /// </summary>
        public Queue<DiscoveredServer> DiscoveredServers { get; } = new Queue<DiscoveredServer>();

        private void SendDiscoverMessage()
        {
            try
            {
                _udpSocket.SendTo(_sendBuffer, new IPEndPoint(IPAddress.Broadcast, 7359));
                Debug.WriteLine("Sent message");
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Broadcast errored with message: {ex.Message}");
            }
        }

        private void ReceiveDiscoveryMessages()
        {
            while (!_disposed)
            {
                try
                {
                    var buffer = new byte[_udpSocket.ReceiveBufferSize];
                    _udpSocket.Receive(buffer);
                    var text = Encoding.ASCII.GetString(buffer, 0, buffer.Length).Replace("\0", string.Empty);
                    Debug.WriteLine($"Received: {text}");

                    var discoveredServer = JsonSerializer.Deserialize<DiscoveredServer>(text);
                    DiscoveredServers.Enqueue(discoveredServer);
                    OnDiscover?.Invoke();
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.TimedOut:
                            Debug.WriteLine($"Socket Timed out");
                            break;
                        case SocketError.Interrupted:
                            // Socket disposed
                            Debug.WriteLine("Socket interrupted.");
                            return;
                        default:
                            Debug.WriteLine($"Socket Error: {ex.Message}");
                            throw ex;
                    }
                }
            }
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public void Dispose()
        {
            _discoveryTimer.Cancel();
            _udpSocket.Dispose();
            _disposed = true;
        }
    }
}
