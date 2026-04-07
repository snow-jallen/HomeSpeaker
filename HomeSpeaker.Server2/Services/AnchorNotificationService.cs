using HomeSpeaker.Server2.Hubs;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.SignalR;

namespace HomeSpeaker.Server2.Services;

public class AnchorNotificationService : IAnchorNotificationService
{
    private readonly IHubContext<AnchorHub> hubContext;
    private readonly ILogger<AnchorNotificationService> logger;

    public AnchorNotificationService(IHubContext<AnchorHub> hubContext, ILogger<AnchorNotificationService> logger)
    {
        this.hubContext = hubContext;
        this.logger = logger;
    }

    public async Task NotifyAnchorDefinitionCreated(AnchorDefinition anchorDefinition)
    {
        logger.LogInformation("Broadcasting anchor definition created: {name}", anchorDefinition.Name);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionCreated", anchorDefinition);
    }

    public async Task NotifyAnchorDefinitionUpdated(AnchorDefinition anchorDefinition)
    {
        logger.LogInformation("Broadcasting anchor definition updated: {name}", anchorDefinition.Name);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionUpdated", anchorDefinition);
    }

    public async Task NotifyAnchorDefinitionDeactivated(int anchorDefinitionId)
    {
        logger.LogInformation("Broadcasting anchor definition deactivated: {id}", anchorDefinitionId);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("AnchorDefinitionDeactivated", anchorDefinitionId);
    }

    public async Task NotifyUserAnchorAssigned(UserAnchor userAnchor)
    {
        logger.LogInformation("Broadcasting user anchor assigned: user {userId}, anchor {anchorId}", userAnchor.UserId, userAnchor.AnchorDefinitionId);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("UserAnchorAssigned", userAnchor);
    }

    public async Task NotifyUserAnchorRemoved(string userId, int anchorDefinitionId)
    {
        logger.LogInformation("Broadcasting user anchor removed: user {userId}, anchor {anchorId}", userId, anchorDefinitionId);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("UserAnchorRemoved", userId, anchorDefinitionId);
    }

    public async Task NotifyDailyAnchorCompletionUpdated(int dailyAnchorId, bool isCompleted, DateTime? completedAt)
    {
        logger.LogInformation("Broadcasting daily anchor completion updated: {dailyAnchorId}, completed: {isCompleted}", dailyAnchorId, isCompleted);
        await hubContext.Clients.Group("AnchorUpdates").SendAsync("DailyAnchorCompletionUpdated", dailyAnchorId, isCompleted, completedAt);
    }
}