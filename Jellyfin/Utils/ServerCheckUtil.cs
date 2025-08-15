using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;

namespace Jellyfin.Utils;

/// <summary>
/// Defines utility methods for checking if a given URL points to a valid Jellyfin server.
/// </summary>
public static class ServerCheckUtil
{
    private const string WebUIBasePath = "/web/";
    private const string ApiSystemInfoRoute = "/System/Info/Public";
    private const string ValidProductName = "Jellyfin Server";

    /// <summary>
    /// Gets or sets a value indicating whether the server version is unsupported in the future by this client version.
    /// </summary>
    public static bool IsFutureUnsupportedVersion { get; set; } = false;

    /// <summary>
    /// Asynchronously validates whether the specified URL points to a valid Jellyfin server.
    /// </summary>
    /// <param name="serverUri">The URL to validate as a string.</param>
    /// <returns><see langword="true"/> if the URL is valid and points to a Jellyfin server; otherwise, <see
    /// langword="false"/>.</returns>
    public static async Task<JellyfinServerValidationResult> IsJellyfinServerUrlValidAsync(Uri serverUri)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, serverUri);
            using var headResponse = await httpClient.SendAsync(headRequest).ConfigureAwait(true);
            var finalUri = headResponse.RequestMessage.RequestUri;

            // Jellyfin redirects to a web root path, which is not a valid base path for the API.
            string basePath = finalUri.ToString();
            if (basePath.EndsWith(WebUIBasePath, StringComparison.OrdinalIgnoreCase))
            {
                basePath = basePath.Substring(0, basePath.Length - WebUIBasePath.Length);
            }

            var infoUri = new Uri(basePath + ApiSystemInfoRoute);

            using var response = await httpClient.GetAsync(infoUri).ConfigureAwait(true);

            if (!response.IsSuccessStatusCode)
            {
                return new JellyfinServerValidationResult(
                    false,
                    $"Failed to connect to the server at \"{serverUri}\". Status code: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            // Check if the response is from a supported Jellyfin server.
            return ValidateJellyfinServerResponse(content);
        }
        catch (HttpRequestException ex)
        {
            return new JellyfinServerValidationResult(
                false,
                $"Could not connect to the server at \"{serverUri}\". Error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new JellyfinServerValidationResult(
                false,
                "The request timed out. Please check your network connection and try again.");
        }
        catch (Exception ex) // Catch any other unexpected exceptions.
        {
            return new JellyfinServerValidationResult(
                false,
                $"An unexpected error occurred while validating the server: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the JSON response from the Jellyfin server to ensure it is a supported server.
    /// </summary>
    /// <param name="serverInfoResponse">The JSON response string to evaluate.</param>
    /// <returns>(IsValid, ErrorMessage): Indicates whether the response is valid and provides an error message if not.</returns>
    private static JellyfinServerValidationResult ValidateJellyfinServerResponse(string serverInfoResponse)
    {
        try
        {
            using var json = JsonDocument.Parse(serverInfoResponse);

            // Making sure we are talking with a Jellyfin server.
            if (json.RootElement.TryGetProperty("ProductName", out var productNameProperty))
            {
                var productName = productNameProperty.GetString();
                if (!string.Equals(productName, ValidProductName, StringComparison.OrdinalIgnoreCase))
                {
                    return new JellyfinServerValidationResult(false, $"ProductName '{productName}' does not match '{ValidProductName}'.");
                }
            }

            // Check if the server version is at least the minimum supported version that this client supports.
            if (json.RootElement.TryGetProperty("Version", out var versionProperty))
            {
                string versionString = versionProperty.GetString();
                if (!Version.TryParse(versionString, out var serverVersion))
                {
                    return new JellyfinServerValidationResult(false, $"Invalid server version format: '{versionString}'.");
                }

                IsFutureUnsupportedVersion = serverVersion < Central.MinimumFutureSupportedServerVersion;
                Central.ServerVersion = serverVersion;

                if (serverVersion < Central.MinimumSupportedServerVersion)
                {
                    return new JellyfinServerValidationResult(false, $"The minimum supported server version is {Central.MinimumSupportedServerVersion}, but the server is running {serverVersion}.");
                }

                return new JellyfinServerValidationResult(true);
            }
        }
        catch (Exception ex)
        {
            return new JellyfinServerValidationResult(false, $"Exception parsing server response: {ex.Message}");
        }

        return new JellyfinServerValidationResult(false, "Unknown error validating server response.");
    }
}
