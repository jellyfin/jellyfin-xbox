using Jellyfin.Core;
using Jellyfin.Models;
using Jellyfin.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Jellyfin.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OnBoarding : Page
    {
        private ObservableCollection<DiscoveredServer> _discoveredServers = new ObservableCollection<DiscoveredServer>();
        private List<Socket> _sockets = new List<Socket>();
        private ServerDiscovery _serverDiscovery = new ServerDiscovery();

        public OnBoarding()
        {
            this.InitializeComponent();
            this.Loaded += OnBoarding_Loaded;
            btnConnect.Click += BtnConnect_Click;
            txtUrl.KeyUp += TxtUrl_KeyUp;
            _serverDiscovery.OnDiscover += _serverDiscovery_OnDiscover;
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
                //If the UriBuilder fails the following functions will handle the error
            }

            await TryConnect(uriString);

            btnConnect.IsEnabled = true;
        }

        private void OnBoarding_Loaded(object sender, RoutedEventArgs e)
        {
            txtUrl.Focus(FocusState.Programmatic);
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

        private async Task TryConnect(string uriString)
        {
            if (!await CheckURLValidAsync(uriString))
            {
                txtError.Visibility = Visibility.Visible;
            }
            else
            {
                Central.Settings.JellyfinServer = uriString;
                (Window.Current.Content as Frame).Navigate(typeof(MainPage));
            }
        }

        private async Task<bool> CheckURLValidAsync(string uriString)
        {
            // also do a check for valid url
            if (!Uri.IsWellFormedUriString(uriString, UriKind.Absolute))
            {
                return false;
            }

            //add scheme to uri if not included 
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

        private async void DiscoveredList_ItemClick(object clickedItem, ItemClickEventArgs e)
        {
            var discoveredServer = (DiscoveredServer) e.ClickedItem;
            var addressString = discoveredServer.Address.ToString();
            txtUrl.Text = addressString;
            await TryConnect(addressString);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            foreach (var socket in _sockets)
            {
                socket.Dispose();
            }
        }
    }
}