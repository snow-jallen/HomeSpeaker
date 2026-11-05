namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>A row of the old <c>UserAnchors</c> table joined to its definition.</summary>
public sealed record LegacyAssignment(string UserId, int DefinitionId, string Name, bool DefinitionActive, DateTime AssignedAt);
