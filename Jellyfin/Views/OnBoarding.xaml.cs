using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;

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
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<OnBoardingViewModel>();
    }
}
