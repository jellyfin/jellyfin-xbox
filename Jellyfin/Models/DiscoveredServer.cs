using System;

namespace Jellyfin.Models;

/// <summary>
/// Represents the server metadata provided by JellyfinServer after autodiscover.
/// </summary>
public class DiscoveredServer : IComparable<DiscoveredServer>, IEquatable<DiscoveredServer>
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
        if (other is null)
        {
            return 1;
        }

        return string.Compare(Id, other.Id, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public bool Equals(DiscoveredServer other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        return Equals(obj as DiscoveredServer);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
    }
}
