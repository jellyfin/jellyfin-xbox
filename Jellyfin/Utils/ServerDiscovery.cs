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
        private readonly TimeSpan _discoverInterval = TimeSpan.FromSeconds(10);
        private readonly ThreadPoolTimer _discoveryTimer;
        private readonly byte[] _sendBuffer = Encoding.ASCII.GetBytes("Who is JellyfinServer?");

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerDiscovery"/> class.
        /// </summary>
        public ServerDiscovery()
        {
            _discoveryTimer = ThreadPoolTimer.CreatePeriodicTimer(DiscoveryPolling_Timer, _discoverInterval);
            new Thread(() => { DiscoverServers(); }).Start();
        }

        /// <summary>
        /// Triggered when a server has been discovered on the local network
        /// </summary>
        public event Action OnDiscover;

        /// <summary>
        /// Gets the queue of local servers that have not yet been retreived.
        /// </summary>
        public Queue<DiscoveredServer> DiscoveredServers { get; } = new Queue<DiscoveredServer>();

        private void DiscoveryPolling_Timer(object sender)
        {
            DiscoverServers();
        }

        private void DiscoverServers()
        {
            using (var udpSocket = new Socket(SocketType.Dgram, ProtocolType.Udp))
            {
                Debug.WriteLine("New UDPSocket");
                udpSocket.EnableBroadcast = true;
                udpSocket.ReceiveTimeout = (int)_discoverInterval.TotalMilliseconds;

                var broadcastAddress = IPAddress.Broadcast;
                var broadcastEndpoint = new IPEndPoint(broadcastAddress, 7359);
                udpSocket.SendTo(_sendBuffer, broadcastEndpoint);
                Debug.WriteLine($"Sent to {broadcastAddress}");

                var recieveBuffer = new byte[256];
                try
                {
                    while (true)
                    {
                        udpSocket.Receive(recieveBuffer);
                        var receivedText = Encoding.ASCII.GetString(recieveBuffer, 0, recieveBuffer.Length)
                            .Replace("\0", string.Empty);
                        Debug.WriteLine($"Received: {receivedText}");
                        var discoveredServer = JsonSerializer.Deserialize<DiscoveredServer>(receivedText);
                        DiscoveredServers.Enqueue(discoveredServer);
                        OnDiscover?.Invoke();
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Debug.WriteLine($"Socket Timed out after {_discoverInterval.TotalSeconds}s");
                    }
                    else
                    {
                        Debug.WriteLine($"Broadcast Address: {broadcastAddress}, errored with message: {ex.Message}");
                        throw ex;
                    }
                }
            }

            Debug.WriteLine("UDPSocket ended");
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public void Dispose()
        {
            _discoveryTimer.Cancel();
        }
    }
}
