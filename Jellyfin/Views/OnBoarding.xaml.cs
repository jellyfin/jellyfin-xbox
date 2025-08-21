using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Helpers;
using Jellyfin.Utils;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Views;

/// <summary>
/// Represents the onboarding page for the application, allowing users to connect to a Jellyfin server.
/// </summary>
public sealed partial class OnBoarding : Page, IDisposable
{
    private ObservableCollection<DiscoveredServer> _discoveredServers = new ObservableCollection<DiscoveredServer>();
    private ServerDiscovery _serverDiscovery = new ServerDiscovery();

    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoarding"/> class.
    /// </summary>
    public OnBoarding()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<OnBoardingViewModel>();
    }
}
