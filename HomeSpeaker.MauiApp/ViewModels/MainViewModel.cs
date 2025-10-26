using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.MauiApp.Services;
using HomeSpeaker.MauiApp.Views;
using HomeSpeaker.Shared;
using System.Collections.ObjectModel;

namespace HomeSpeaker.MauiApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServerConfigurationService _serverConfigService;
    private readonly IHomeSpeakerClientService _clientService;

    [ObservableProperty]
    private ObservableCollection<ServerConfiguration> _servers = new();

    [ObservableProperty]
    private ServerConfiguration? _selectedServer;

    [ObservableProperty]
    private ObservableCollection<PlaylistMessage> _playlists = new();

    [ObservableProperty]
    private PlaylistMessage? _selectedPlaylist;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public MainViewModel(IServerConfigurationService serverConfigService, IHomeSpeakerClientService clientService)
    {
        _serverConfigService = serverConfigService;
        _clientService = clientService;
    }

    [RelayCommand]
    private async Task LoadServersAsync()
    {
        IsLoading = true;
        try
        {
            var servers = await _serverConfigService.GetServersAsync();
            Servers.Clear();
            foreach (var server in servers)
            {
                Servers.Add(server);
            }

            if (Servers.Any() && SelectedServer == null)
            {
                SelectedServer = Servers.FirstOrDefault(s => s.IsDefault) ?? Servers.First();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading servers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        if (SelectedServer == null)
        {
            StatusMessage = "Please select a server first";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading playlists...";

        try
        {
            var playlists = await _clientService.GetPlaylistsAsync(SelectedServer.ServerUrl);
            Playlists.Clear();
            foreach (var playlist in playlists)
            {
                Playlists.Add(playlist);
            }
            StatusMessage = $"Loaded {playlists.Count} playlists";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading playlists: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayPlaylistAsync(PlaylistMessage? playlist)
    {
        if (SelectedServer == null)
        {
            StatusMessage = "Please select a server first";
            return;
        }

        if (playlist == null)
        {
            StatusMessage = "Please select a playlist";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Playing playlist: {playlist.PlaylistName}";

        try
        {
            await _clientService.PlayPlaylistAsync(SelectedServer.ServerUrl, playlist.PlaylistName);
            StatusMessage = $"Now playing: {playlist.PlaylistName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error playing playlist: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        await Shell.Current.GoToAsync(nameof(ServerConfigPage));
    }

    partial void OnSelectedServerChanged(ServerConfiguration? value)
    {
        if (value != null)
        {
            _ = LoadPlaylistsAsync();
        }
    }
}
