using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;
using Jellyfin.Helpers;
using Jellyfin.Models;
using Jellyfin.Utils;
using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Views;

/// <summary>
/// Represents the onboarding page for the application, allowing users to connect to a Jellyfin server.
/// </summary>
public sealed partial class OnBoarding : Page
{
    private readonly OnBoardingViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoarding"/> class.
    /// </summary>
    public OnBoarding()
    {
        InitializeComponent();
        DataContext = _viewModel = App.Current.Services.GetRequiredService<OnBoardingViewModel>();
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        _viewModel.ConnectToDiscoveredServer((DiscoveredServer)e.ClickedItem);
    }
}
