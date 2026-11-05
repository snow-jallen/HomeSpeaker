namespace HomeSpeaker.Server2.Services.Anchors;

public sealed record PlannedAnchor(string Title, IReadOnlyList<AnchorPeriod> Periods, IReadOnlyList<PlannedCompletion> Completed);
