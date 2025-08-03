using System;

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
    /// (isValid, uri, errorMessage): Whether the input is valid, the parsed Uri if valid, and an error message if not.
    /// </returns>
    public static (bool IsValid, Uri Uri, string ErrorMessage) ParseServerUri(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (false, null, "Please enter a server address.");
        }

        input = input.Trim();

        // Check for scheme separator "://" to determine if the input has a scheme.
        int schemeSeparatorIndex = input.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex == 0)
        {
            return (false, null, "Please enter a valid HTTP or HTTPS URL scheme.");
        }

        // If no scheme is present, default to http://.
        if (schemeSeparatorIndex < 0)
        {
            input = $"{Uri.UriSchemeHttp}://{input}";
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return (false, null, "Please enter a valid server URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return (false, null, "Please enter a valid HTTP or HTTPS URL scheme.");
        }

        return (true, uri, null);
    }
}
