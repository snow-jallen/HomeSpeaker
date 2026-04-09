using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

public interface IAnchorNotificationService
{
    Task NotifyAnchorDefinitionCreated(AnchorDefinition anchorDefinition);
    Task NotifyAnchorDefinitionUpdated(AnchorDefinition anchorDefinition);
    Task NotifyAnchorDefinitionDeactivated(int anchorDefinitionId);
    Task NotifyUserAnchorAssigned(UserAnchor userAnchor);
    Task NotifyUserAnchorRemoved(string userId, int anchorDefinitionId);
    Task NotifyDailyAnchorCompletionUpdated(int dailyAnchorId, bool isCompleted, DateTime? completedAt);
}