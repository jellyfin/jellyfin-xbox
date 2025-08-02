using System;
using System.Net;
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
        await BtnConnect_ClickAsync();
    }

    private async Task BtnConnect_ClickAsync()
    {
        btnConnect.IsEnabled = false;
        txtError.Visibility = Visibility.Collapsed;

        string inputUrl = txtUrl.Text;

        // Parse the input URL to validate and normalize it.
        var (isValid, normalizedUrl, errorMessage) = UrlValidator.ParseServerUri(inputUrl);
        if (!isValid)
        {
            txtError.Text = errorMessage;
            txtError.Visibility = Visibility.Visible;
            btnConnect.IsEnabled = true;
            return;
        }

        if (!await IsJellyfinServerUrlValidAsync(normalizedUrl))
        {
            txtError.Visibility = Visibility.Visible;
        }
        else
        {
            Central.Settings.JellyfinServer = normalizedUrl.ToString();
            (Window.Current.Content as Frame).Navigate(typeof(MainPage));
        }

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
        // check URL exists
        HttpWebRequest request;
        HttpWebResponse response;
        try
        {
            request = (HttpWebRequest)WebRequest.Create(serverUri);
            response = (HttpWebResponse)(await request.GetResponseAsync());
        }
        catch (WebException ex)
        {
            // Handle web exceptions here
            if (ex.Response != null && ex.Response is HttpWebResponse errorResponse)
            {
                int statusCode = (int)errorResponse.StatusCode;
                if (statusCode >= 300 && statusCode <= 308)
                {
                    // Handle Redirect
                    string newLocation = errorResponse.Headers["Location"];
                    if (!string.IsNullOrEmpty(newLocation))
                    {
                        Uri newUri;
                        try
                        {
                            newUri = new Uri(serverUri, newLocation);
                        }
                        catch (UriFormatException)
                        {
                            txtError.Visibility = Visibility.Visible;
                            txtError.Text = "Invalid redirect URL received from server in Location header.";
                            btnConnect.IsEnabled = true;
                            return false;
                        }
                    }
                }
                else
                {
                    UpdateErrorMessage(statusCode);
                }

                return false;
            }
            else
            {
                // Handle other exceptions
                return false;
            }
        }

        if (response == null || response.StatusCode != HttpStatusCode.OK)
        {
            return false;
        }

        var encoding = System.Text.Encoding.GetEncoding(response.CharacterSet);
        using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
        {
            string responseText = reader.ReadToEnd();
            if (!responseText.Contains("Jellyfin"))
            {
                return false;
            }
        }

        // If everything is OK, update the URI before saving it
        Central.Settings.JellyfinServer = serverUri.ToString();
        return true;
    }

    private void UpdateErrorMessage(int statusCode)
    {
        txtError.Visibility = Visibility.Visible;
        txtError.Text = $"Error: {statusCode}";
    }
}
