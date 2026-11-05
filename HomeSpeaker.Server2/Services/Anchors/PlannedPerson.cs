namespace HomeSpeaker.Server2.Services.Anchors;

public sealed record PlannedPerson(string Name, IReadOnlyList<PlannedAnchor> Anchors);
