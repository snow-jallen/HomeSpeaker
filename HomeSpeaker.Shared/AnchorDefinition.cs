using System;

namespace HomeSpeaker.Shared;

/// <summary>
/// Represents an anchor definition that can be assigned to users
/// </summary>
public record AnchorDefinition(int Id, string Name, string Description, bool IsActive);

/// <summary>
/// Represents a user's current active anchors (template)
/// </summary>
public record UserAnchor(int Id, string UserId, int AnchorDefinitionId, DateTime CreatedAt);

/// <summary>
/// Represents a daily snapshot of a user's anchors with completion status (temporal record)
/// </summary>
public record DailyAnchor(int Id, string UserId, int AnchorDefinitionId, DateOnly Date, bool IsCompleted, DateTime CompletedAt, string AnchorName, string AnchorDescription);

/// <summary>
/// DTO for creating/updating anchor definitions
/// </summary>
public record CreateAnchorDefinitionRequest(string Name, string Description);

/// <summary>
/// DTO for updating anchor completion status
/// </summary>
public record UpdateAnchorCompletionRequest(int DailyAnchorId, bool IsCompleted);

/// <summary>
/// DTO for assigning anchors to a user
/// </summary>
public record AssignAnchorToUserRequest(string UserId, int AnchorDefinitionId);
