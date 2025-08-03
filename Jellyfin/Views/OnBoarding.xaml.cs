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

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
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

            // Check if the response is a Jellyfin server response.
            if (!IsJellyfinServerResponse(content))
            {
                txtError.Visibility = Visibility.Visible;
                txtError.Text = "Jellyfin server not found, is it online?";
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
    /// Determines whether the provided JSON string represents a response from a Jellyfin Server.
    /// </summary>
    /// <param name="jsonContent">The JSON response string to evaluate.</param>
    /// <returns><see langword="true"/> if the response is from a Jellyfin Server; otherwise,
    /// <see langword="false"/>.</returns>
    private static bool IsJellyfinServerResponse(string jsonContent)
    {
        try
        {
            using var json = JsonDocument.Parse(jsonContent);

            // Making sure we are talking with a Jellyfin server.
            if (json.RootElement.TryGetProperty("ProductName", out var productNameProperty))
            {
                string productName = productNameProperty.GetString();
                return string.Equals(productName, ValidProductName, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private void UpdateErrorMessage(int statusCode)
    {
        txtError.Visibility = Visibility.Visible;
        txtError.Text = $"Error: {statusCode}";
    }
}
