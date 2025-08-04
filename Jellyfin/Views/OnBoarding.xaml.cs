using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Helpers;
using Jellyfin.Utils;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Jellyfin.Views;

/// <summary>
/// Represents the onboarding page for the application, allowing users to connect to a Jellyfin server.
/// </summary>
public sealed partial class OnBoarding : Page
{
    private ObservableCollection<DiscoveredServer> _discoveredServers = new ObservableCollection<DiscoveredServer>();
    private ServerDiscovery _serverDiscovery = new ServerDiscovery();

    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoarding"/> class.
    /// </summary>
    public OnBoarding()
    {
        this.InitializeComponent();
        this.Loaded += OnBoarding_Loaded;
        txtUrl.KeyUp += TxtUrl_KeyUp;
        txtUrl.Text = Central.Settings.JellyfinServer ?? string.Empty;
        _serverDiscovery.OnDiscover += _serverDiscovery_OnDiscover;
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
                var jellyfinServerCheck = await ServerCheckUtil.IsJellyfinServerUrlValidAsync(parsedUri).ConfigureAwait(true);
                // Check if the parsed URI is pointing to a Jellyfin server.
                if (!jellyfinServerCheck.IsValid)
                {
                    txtError.Text = jellyfinServerCheck.ErrorMessage;
                    txtError.Visibility = Visibility.Visible;
                    btnConnect.IsEnabled = true;
                    return;
                }

                // Save validated URL and navigate to page containing the web view.
                Central.Settings.JellyfinServer = parsedUri.ToString();
                Central.Settings.JellyfinServerValidated = true;
                (Window.Current.Content as Frame).Navigate(typeof(MainPage));

                btnConnect.IsEnabled = true;
            }
            finally
            {
                ProgressIndicator.Visibility = Visibility.Collapsed;
            }
        });
    }

    private async void _serverDiscovery_OnDiscover()
    {
        DiscoveredServer discoveredServer = null;
        while (_serverDiscovery.DiscoveredServers.TryDequeue(out discoveredServer))
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (!_discoveredServers.Contains(discoveredServer))
                {
                    _discoveredServers.Add(discoveredServer);
                    txtDiscoverNoneFound.Visibility = Visibility.Collapsed;
                }
            });
        }
    }

    private void TxtUrl_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            BtnConnect_Click(btnConnect, null);
        }

    }

    private async void DiscoveredList_ItemClick(object clickedItem, ItemClickEventArgs e)
    {
        var discoveredServer = (DiscoveredServer) e.ClickedItem;
        var addressString = discoveredServer.Address.ToString();
        txtUrl.Text = addressString;
        await TryConnect(addressString).ConfigureAwait(false);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _serverDiscovery.Dispose();
    }
}
