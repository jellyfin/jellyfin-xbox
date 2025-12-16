using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Models;

/// <summary>
/// Reperesents the server metadata provided by JellyfinServer after autodiscover.
/// </summary>
public class DiscoveredServer : IComparable<DiscoveredServer>
{
    /// <summary>
    /// Gets or sets the name of the server.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique ID of the server.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the Main address of the server.
    /// </summary>
    public Uri Address { get; set; }

    /// <summary>
    /// Gets or sets the endpoint address of the server.
    /// </summary>
    public Uri EndpointAddress { get; set; }

    /// <inheritdoc/>
    public int CompareTo(DiscoveredServer other)
    {
        return Id.CompareTo(other.Id);
    }
}
