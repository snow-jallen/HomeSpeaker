using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.MauiApp.Services;

namespace HomeSpeaker.MauiApp.ViewModels;

public partial class ServerConfigViewModel : ObservableObject
{
    private readonly IServerConfigurationService _serverConfigService;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ServerConfigViewModel(IServerConfigurationService serverConfigService)
    {
        _serverConfigService = serverConfigService;
    }

    [RelayCommand]
    private async Task SaveServerAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Nickname))
        {
            ErrorMessage = "Please enter a nickname";
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "Please enter a server URL";
            return;
        }

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
        {
            ErrorMessage = "Please enter a valid URL (e.g., https://example.com:5001)";
            return;
        }

        try
        {
            var server = new ServerConfiguration
            {
                Nickname = Nickname,
                ServerUrl = ServerUrl,
                IsDefault = IsDefault
            };

            await _serverConfigService.AddServerAsync(server);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving server: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
