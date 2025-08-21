using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Controls;

/// <summary>
/// Represents a custom web view control for interacting with a Jellyfin server.
/// </summary>
public sealed partial class JellyfinWebView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinWebView"/> class.
    /// </summary>
    public JellyfinWebView()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<JellyfinWebViewModel>();
    }
}
