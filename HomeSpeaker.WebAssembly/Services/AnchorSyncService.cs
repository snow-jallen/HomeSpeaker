using Microsoft.AspNetCore.SignalR.Client;

namespace HomeSpeaker.WebAssembly.Services;

public class AnchorSyncService : IAnchorSyncService
{
    private readonly IConfiguration configuration;
    private readonly ILogger<AnchorSyncService> logger;
    private HubConnection? hubConnection;

    public AnchorSyncService(IConfiguration configuration, ILogger<AnchorSyncService> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public bool IsConnected => hubConnection?.State == HubConnectionState.Connected;

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
            var anchorsApiAddress = configuration["AnchorsApiAddress"] ?? "http://localhost";

            hubConnection = new HubConnectionBuilder()
                .WithUrl($"{anchorsApiAddress}/anchorHub")
                .Build();

            // Subscribe to hub events
            hubConnection.On<AnchorDefinition>("AnchorDefinitionCreated", (anchorDefinition) =>
            {
                logger.LogInformation("Received anchor definition created: {Name}", anchorDefinition.Name);
                OnAnchorDefinitionCreated?.Invoke(anchorDefinition);
            });

            hubConnection.On<AnchorDefinition>("AnchorDefinitionUpdated", (anchorDefinition) =>
            {
                logger.LogInformation("Received anchor definition updated: {Name}", anchorDefinition.Name);
                OnAnchorDefinitionUpdated?.Invoke(anchorDefinition);
            });

            hubConnection.On<int>("AnchorDefinitionDeactivated", (anchorDefinitionId) =>
            {
                logger.LogInformation("Received anchor definition deactivated: {Id}", anchorDefinitionId);
                OnAnchorDefinitionDeactivated?.Invoke(anchorDefinitionId);
            });

            hubConnection.On<UserAnchor>("UserAnchorAssigned", (userAnchor) =>
            {
                logger.LogInformation("Received user anchor assigned: user {UserId}, anchor {AnchorId}", userAnchor.UserId, userAnchor.AnchorDefinitionId);
                OnUserAnchorAssigned?.Invoke(userAnchor);
            });

            hubConnection.On<string, int>("UserAnchorRemoved", (userId, anchorDefinitionId) =>
            {
                logger.LogInformation("Received user anchor removed: user {UserId}, anchor {AnchorId}", userId, anchorDefinitionId);
                OnUserAnchorRemoved?.Invoke(userId, anchorDefinitionId);
            });

            hubConnection.On<int, bool, DateTime?>("DailyAnchorCompletionUpdated", (dailyAnchorId, isCompleted, completedAt) =>
            {
                logger.LogInformation("Received daily anchor completion updated: {DailyAnchorId}, completed: {IsCompleted}", dailyAnchorId, isCompleted);
                OnDailyAnchorCompletionUpdated?.Invoke(dailyAnchorId, isCompleted, completedAt);
            });

            // Connection closed handler
            hubConnection.Closed += async (error) =>
            {
                if (error != null)
                {
                    logger.LogWarning("SignalR connection closed with error: {Error}", error.Message);
                }
                else
                {
                    logger.LogInformation("SignalR connection closed");
                }

                // Reconnect after 5 seconds
                await Task.Delay(5000);
                await StartAsync();
            };

            // Start connection
            await hubConnection.StartAsync();
            logger.LogInformation("SignalR connection started successfully to {Url}", $"{anchorsApiAddress}/anchorHub");

            // Join the anchor updates group
            await hubConnection.SendAsync("JoinAnchorGroup");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start SignalR connection");
        }
    }

    public async Task StopAsync()
    {
        if (hubConnection is not null)
        {
            try
            {
                await hubConnection.SendAsync("LeaveAnchorGroup");
                await hubConnection.StopAsync();
                logger.LogInformation("SignalR connection stopped");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping SignalR connection");
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ = hubConnection?.DisposeAsync();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}