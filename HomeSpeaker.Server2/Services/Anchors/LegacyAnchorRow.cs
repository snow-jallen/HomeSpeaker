namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>A row of the old <c>DailyAnchors</c> table: the day's snapshot of one person's anchor.</summary>
public sealed record LegacyAnchorRow(string UserId, int DefinitionId, string Name, DateOnly Date, bool Completed, DateTime? CompletedAt);
