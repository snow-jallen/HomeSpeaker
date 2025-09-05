using HomeSpeaker.Server2.Hubs;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.SignalR;

namespace HomeSpeaker.Server2.Services;

public class AnchorNotificationService : IAnchorNotificationService
{
    private readonly IHubContext<AnchorHub> _hubContext;
    private readonly ILogger<AnchorNotificationService> _logger;

    public AnchorNotificationService(IHubContext<AnchorHub> hubContext, ILogger<AnchorNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAnchorDefinitionCreated(AnchorDefinition anchorDefinition)
    {
        _logger.LogInformation("Broadcasting anchor definition created: {name}", anchorDefinition.Name);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionCreated", anchorDefinition);
    }

    public async Task NotifyAnchorDefinitionUpdated(AnchorDefinition anchorDefinition)
    {
        _logger.LogInformation("Broadcasting anchor definition updated: {name}", anchorDefinition.Name);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionUpdated", anchorDefinition);
    }

    public async Task NotifyAnchorDefinitionDeactivated(int anchorDefinitionId)
    {
        _logger.LogInformation("Broadcasting anchor definition deactivated: {id}", anchorDefinitionId);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionDeactivated", anchorDefinitionId);
    }

    public async Task NotifyUserAnchorAssigned(UserAnchor userAnchor)
    {
        _logger.LogInformation("Broadcasting user anchor assigned: user {userId}, anchor {anchorId}", userAnchor.UserId, userAnchor.AnchorDefinitionId);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("UserAnchorAssigned", userAnchor);
    }

    public async Task NotifyUserAnchorRemoved(string userId, int anchorDefinitionId)
    {
        _logger.LogInformation("Broadcasting user anchor removed: user {userId}, anchor {anchorId}", userId, anchorDefinitionId);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("UserAnchorRemoved", userId, anchorDefinitionId);
    }

    public async Task NotifyDailyAnchorCompletionUpdated(int dailyAnchorId, bool isCompleted, DateTime? completedAt)
    {
        _logger.LogInformation("Broadcasting daily anchor completion updated: {dailyAnchorId}, completed: {isCompleted}", dailyAnchorId, isCompleted);
        await _hubContext.Clients.Group("AnchorUpdates").SendAsync("DailyAnchorCompletionUpdated", dailyAnchorId, isCompleted, completedAt);
    }
}