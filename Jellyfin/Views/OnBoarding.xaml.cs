using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Jellyfin.Views;

/// <summary>
/// Represents the onboarding page for the application, allowing users to connect to a Jellyfin server.
/// </summary>
public sealed partial class OnBoarding : Page
{
    private const string WebUIBasePath = "/web/";
    private const string ApiSystemInfoRoute = "/System/Info/Public";
    private const string ValidProductName = "Jellyfin Server";

    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoarding"/> class.
    /// </summary>
    public OnBoarding()
    {
        this.InitializeComponent();
        this.Loaded += OnBoarding_Loaded;
        txtUrl.KeyUp += TxtUrl_KeyUp;
    }

    private void OnBoarding_Loaded(object sender, RoutedEventArgs e)
    {
        txtUrl.Focus(FocusState.Programmatic);
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        btnConnect.IsEnabled = false;
        txtError.Visibility = Visibility.Collapsed;

        string inputUrl = txtUrl.Text;

        // Parse the input URL to validate and normalize it.
        var (isValid, parsedUri, errorMessage) = UrlValidator.ParseServerUri(inputUrl);
        if (!isValid)
        {
            txtError.Text = errorMessage;
            txtError.Visibility = Visibility.Visible;
            btnConnect.IsEnabled = true;
            return;
        }

        ProgressIndicator.Visibility = Visibility.Visible;

        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
        {
            try
            {
                // Check if the parsed URI is pointing to a Jellyfin server.
                if (!await IsJellyfinServerUrlValidAsync(parsedUri))
                {
                    txtError.Visibility = Visibility.Visible;
                    btnConnect.IsEnabled = true;
                    return;
                }

                // Save validated URL and navigate to page containing the web view.
                Central.Settings.JellyfinServer = parsedUri.ToString();
                (Window.Current.Content as Frame).Navigate(typeof(MainPage));

                btnConnect.IsEnabled = true;
            }
            finally
            {
                ProgressIndicator.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void TxtUrl_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            BtnConnect_Click(btnConnect, null);
        }
    }

    /// <summary>
    /// Asynchronously validates whether the specified URL points to a valid Jellyfin server.
    /// </summary>
    /// <param name="serverUri">The URL to validate as a string.</param>
    /// <returns><see langword="true"/> if the URL is valid and points to a Jellyfin server; otherwise, <see
    /// langword="false"/>.</returns>
    private async Task<bool> IsJellyfinServerUrlValidAsync(Uri serverUri)
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
                UpdateErrorMessage((int)response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            // Check if the response is from a supported Jellyfin server.
            var (isValid, errorMessage) = ValidateJellyfinServerResponse(content);
            if (!isValid)
            {
                txtError.Visibility = Visibility.Visible;
                txtError.Text = errorMessage;
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            txtError.Visibility = Visibility.Visible;
            txtError.Text = $"Could not connect to the server at \"{serverUri}\". Error: {ex.Message}";
            return false;
        }
        catch (OperationCanceledException)
        {
            txtError.Visibility = Visibility.Visible;
            txtError.Text = "The request timed out. Please check your network connection and try again.";
            return false;
        }
        catch (Exception ex) // Catch any other unexpected exceptions.
        {
            txtError.Visibility = Visibility.Visible;
            txtError.Text = $"An unexpected error occurred: {ex.Message}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the JSON response from the Jellyfin server to ensure it is a supported server.
    /// </summary>
    /// <param name="serverInfoResponse">The JSON response string to evaluate.</param>
    /// <returns>(IsValid, ErrorMessage): Indicates whether the response is valid and provides an error message if not.</returns>
    private static (bool IsValid, string ErrorMessage) ValidateJellyfinServerResponse(string serverInfoResponse)
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
                    return (false, $"ProductName '{productName}' does not match '{ValidProductName}'.");
                }
            }

            // Check if the server version is at least the minimum supported version that this client supports.
            if (json.RootElement.TryGetProperty("Version", out var versionProperty))
            {
                string versionString = versionProperty.GetString();
                if (!Version.TryParse(versionString, out var serverVersion))
                {
                    return (false, $"Invalid server version format: '{versionString}'.");
                }

                if (serverVersion < Central.MinimumSupportedServerVersion)
                {
                    return (false, $"The minimum supported server version is {Central.MinimumSupportedServerVersion}, but the server is running {serverVersion}.");
                }

                return (true, null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Exception parsing server response: {ex.Message}");
        }

        return (false, "Unknown error validating server response.");
    }

    private void UpdateErrorMessage(int statusCode)
    {
        txtError.Visibility = Visibility.Visible;
        txtError.Text = $"Error: {statusCode}";
    }
}
