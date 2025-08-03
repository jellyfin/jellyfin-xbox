using System;
using System.Net;
using System.Threading.Tasks;
using Jellyfin.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Jellyfin.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class OnBoarding : Page
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoarding"/> class.
    /// </summary>
    public OnBoarding()
    {
        InitializeComponent();
        Loaded += OnBoarding_Loaded;
        btnConnect.Click += BtnConnect_Click;
        txtUrl.KeyUp += TxtUrl_KeyUp;
    }

    private void TxtUrl_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            BtnConnect_Click(btnConnect, null);
        }
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        btnConnect.IsEnabled = false;
        txtError.Visibility = Visibility.Collapsed;

        string uriString = txtUrl.Text;
        try
        {
            var ub = new UriBuilder(uriString);
            uriString = ub.ToString();
        }
        catch
        {
            // If the UriBuilder fails the following functions will handle the error
        }

        if (!await CheckURLValidAsync(uriString))
        {
            txtError.Visibility = Visibility.Visible;
        }
        else
        {
            Central.Settings.JellyfinServer = uriString;
            (Window.Current.Content as Frame).Navigate(typeof(MainPage));
        }

        btnConnect.IsEnabled = true;
    }

    private void OnBoarding_Loaded(object sender, RoutedEventArgs e)
    {
        txtUrl.Focus(FocusState.Programmatic);
    }

    private async Task<bool> CheckURLValidAsync(string uriString)
    {
        // also do a check for valid url
        if (!Uri.IsWellFormedUriString(uriString, UriKind.Absolute))
        {
            return false;
        }

        // add scheme to uri if not included
        Uri testUri = new UriBuilder(uriString).Uri;

        // check URL exists
        HttpWebRequest request;
        HttpWebResponse response;
        try
        {
            request = (HttpWebRequest)WebRequest.Create(testUri);
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
                        uriString = newLocation;
                        return await CheckURLValidAsync(uriString); // Recursively check the new location
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
        Central.Settings.JellyfinServer = uriString;

        return true;
    }

    private void UpdateErrorMessage(int statusCode)
    {
        txtError.Visibility = Visibility.Visible;
        txtError.Text = $"Error: {statusCode}";
    }
}
