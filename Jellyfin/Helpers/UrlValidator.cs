using System;
using System.Collections.Generic;

namespace Jellyfin.Helpers;

/// <summary>
/// Provides methods for validating and constructing server URIs from input strings.
/// </summary>
public static class UrlValidator
{
    /// <summary>
    /// Parses the input string to validate and construct a server URI.
    /// </summary>
    /// <remarks>If the input does not include a scheme, assumes "http://" by default.</remarks>
    /// <param name="input">The server address input as a string. This can include or omit the scheme "http://".</param>
    /// <returns>
    /// (IsValid, Uri, ErrorMessage): Whether the input is valid, the parsed Uri if valid, and an error message if not.
    /// </returns>
    public static (bool IsValid, Uri[] UriCandidates, string ErrorMessage) ParseServerUri(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (false, null, "Please enter a server address.");
        }

        input = input.Trim();

        var uriCandidates = new List<Uri>();

        // check for existing protocol

        if (!Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out var uri))
        {
            return (false, null, "Please enter a valid server URL.");
        }

        uriCandidates.Add(uri);

        if (!uri.IsAbsoluteUri || string.IsNullOrWhiteSpace(uri.Scheme))
        {
            if (!uri.IsAbsoluteUri || uri.Port == 0)
            {
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttps, Port = 443 }.Uri);
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttp, Port = 80 }.Uri);
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttps, Port = 8920 }.Uri);
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttp, Port = 8096 }.Uri);
            }
            else
            {
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttps }.Uri);
                uriCandidates.Add(new UriBuilder(input) { Scheme = Uri.UriSchemeHttp }.Uri);
            }
        }

        return (true, uriCandidates.ToArray(), null);
    }
}
