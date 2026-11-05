namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>An anchor from any source (this device's database or an external feed), ready to be laid out on the board.</summary>
public sealed record SourceAnchor(
    string Id,
    string Title,
    AnchorSlot Slot,
    int Order,
    TimeOnly? TargetTime,
    IReadOnlyList<AnchorPeriod> Periods,
    IReadOnlyDictionary<DateOnly, SourceLog> Logs,
    string Source = "local",
    bool Editable = true,
    bool Writable = true,
    bool MissingIsUnknown = false);
