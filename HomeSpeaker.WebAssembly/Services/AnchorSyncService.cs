using HomeSpeaker.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace HomeSpeaker.WebAssembly.Services;

public class AnchorSyncService : IAnchorSyncService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnchorSyncService> _logger;
    private HubConnection? _hubConnection;

    public AnchorSyncService(IConfiguration configuration, ILogger<AnchorSyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<AnchorDefinition>? OnAnchorDefinitionCreated;
    public event Action<AnchorDefinition>? OnAnchorDefinitionUpdated;
    public event Action<int>? OnAnchorDefinitionDeactivated;
    public event Action<UserAnchor>? OnUserAnchorAssigned;
    public event Action<string, int>? OnUserAnchorRemoved;
    public event Action<int, bool, DateTime?>? OnDailyAnchorCompletionUpdated;

    public async Task StartAsync()
    {
        try
        {
            var anchorsApiAddress = _configuration["AnchorsApiAddress"] ?? "http://localhost";
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{anchorsApiAddress}/anchorHub")
                .Build();

            // Subscribe to hub events
            _hubConnection.On<AnchorDefinition>("AnchorDefinitionCreated", (anchorDefinition) =>
            {
                _logger.LogInformation("Received anchor definition created: {Name}", anchorDefinition.Name);
                OnAnchorDefinitionCreated?.Invoke(anchorDefinition);
            });

            _hubConnection.On<AnchorDefinition>("AnchorDefinitionUpdated", (anchorDefinition) =>
            {
                _logger.LogInformation("Received anchor definition updated: {Name}", anchorDefinition.Name);
                OnAnchorDefinitionUpdated?.Invoke(anchorDefinition);
            });

            _hubConnection.On<int>("AnchorDefinitionDeactivated", (anchorDefinitionId) =>
            {
                _logger.LogInformation("Received anchor definition deactivated: {Id}", anchorDefinitionId);
                OnAnchorDefinitionDeactivated?.Invoke(anchorDefinitionId);
            });

            _hubConnection.On<UserAnchor>("UserAnchorAssigned", (userAnchor) =>
            {
                _logger.LogInformation("Received user anchor assigned: user {UserId}, anchor {AnchorId}", userAnchor.UserId, userAnchor.AnchorDefinitionId);
                OnUserAnchorAssigned?.Invoke(userAnchor);
            });

            _hubConnection.On<string, int>("UserAnchorRemoved", (userId, anchorDefinitionId) =>
            {
                _logger.LogInformation("Received user anchor removed: user {UserId}, anchor {AnchorId}", userId, anchorDefinitionId);
                OnUserAnchorRemoved?.Invoke(userId, anchorDefinitionId);
            });

            _hubConnection.On<int, bool, DateTime?>("DailyAnchorCompletionUpdated", (dailyAnchorId, isCompleted, completedAt) =>
            {
                _logger.LogInformation("Received daily anchor completion updated: {DailyAnchorId}, completed: {IsCompleted}", dailyAnchorId, isCompleted);
                OnDailyAnchorCompletionUpdated?.Invoke(dailyAnchorId, isCompleted, completedAt);
            });

            // Connection closed handler
            _hubConnection.Closed += async (error) =>
            {
                if (error != null)
                {
                    _logger.LogWarning("SignalR connection closed with error: {Error}", error.Message);
                }
                else
                {
                    _logger.LogInformation("SignalR connection closed");
                }

                // Reconnect after 5 seconds
                await Task.Delay(5000);
                await StartAsync();
            };

            // Start connection
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection started successfully to {Url}", $"{anchorsApiAddress}/anchorHub");

            // Join the anchor updates group
            await _hubConnection.SendAsync("JoinAnchorGroup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection is not null)
        {
            try
            {
                await _hubConnection.SendAsync("LeaveAnchorGroup");
                await _hubConnection.StopAsync();
                _logger.LogInformation("SignalR connection stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
            }
        }
    }

    public void Dispose()
    {
        _hubConnection?.DisposeAsync();
    }
}