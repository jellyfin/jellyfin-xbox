using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jellyfin.Core;
using Jellyfin.Helpers;
using Jellyfin.Models;
using Jellyfin.Utils;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.ViewModels;

/// <summary>
/// ViewModel for the OnBoarding page.
/// </summary>
public sealed class OnBoardingViewModel : ObservableObject, IDisposable
{
    private readonly CoreDispatcher _dispatcher;
    private readonly Frame _frame;
    private string _serverUrl;
    private string _errorMessage;
    private bool _isInProgress;
    private ServerDiscovery _serverDiscovery = new ServerDiscovery();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnBoardingViewModel"/> class.
    /// </summary>
    /// <param name="dispatcher">UI Dispatcher.</param>
    /// <param name="frame">Frame for navigation.</param>
    public OnBoardingViewModel(CoreDispatcher dispatcher, Frame frame)
    {
        ConnectCommand = new RelayCommand(ConnectToServerAsync, CanExecuteConnectToServer);
        ServerUrl = Central.Settings.JellyfinServer ?? string.Empty;
        _dispatcher = dispatcher;
        _frame = frame;
        _serverDiscovery.OnDiscover += ServerDiscovery_OnDiscover;
    }

    /// <summary>
    /// Gets or Sets a value indicating whether the connection to the server is in progress.
    /// </summary>
    public bool IsInProgress
    {
        get => _isInProgress;
        set
        {
            if (SetProperty(ref _isInProgress, value))
            {
                ConnectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the error message to display if the connection fails.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Gets or sets the server URL to connect to.
    /// </summary>
    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (SetProperty(ref _serverUrl, value))
            {
                ConnectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the list of discovered servers on the network.
    /// </summary>
    public ObservableCollection<DiscoveredServer> DiscoveredServers { get; } = new ObservableCollection<DiscoveredServer>();

    /// <summary>
    /// Gets the command to connect to the server.
    /// </summary>
    public IRelayCommand ConnectCommand { get; private set; }

    private bool CanExecuteConnectToServer()
    {
        return !string.IsNullOrWhiteSpace(ServerUrl) && !IsInProgress;
    }

    private void ConnectToServerAsync()
    {
        IsInProgress = true;
        ErrorMessage = null;
        var (isValid, parsedUri, errorMessage) = UrlValidator.ParseServerUri(ServerUrl);
        if (!isValid)
        {
            ErrorMessage = errorMessage;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var jellyfinServerCheck = await ServerCheckUtil.IsJellyfinServerUrlValidAsync(parsedUri).ConfigureAwait(true);

                _ = _dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        // Check if the parsed URI is pointing to a Jellyfin server.
                        if (!jellyfinServerCheck.IsValid)
                        {
                            ErrorMessage = jellyfinServerCheck.ErrorMessage;
                            return;
                        }

                        // Save validated URL and navigate to page containing the web view.
                        Central.Settings.JellyfinServer = parsedUri.ToString();
                        Central.Settings.JellyfinServerValidated = true;

                        _frame.Navigate(typeof(MainPage));
                    });
            }
            finally
            {
                _ = _dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        IsInProgress = false;
                    });
            }
        });
    }

    private void ServerDiscovery_OnDiscover()
    {
        var currentServer = _serverDiscovery.DiscoveredServers.Dequeue();
        if (!DiscoveredServers.Contains(currentServer))
        {
            _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DiscoveredServers.Add(currentServer);
            });
        }
    }

    /// <summary>
    /// Connects to a server from the discovery list.
    /// </summary>
    /// <param name="server">A network discovered server.</param>
    public void ConnectToDiscoveredServer(DiscoveredServer server)
    {
        ServerUrl = server.Address.ToString();
        if (CanExecuteConnectToServer())
        {
            ConnectToServerAsync();
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _serverDiscovery.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
