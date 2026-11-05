namespace HomeSpeaker.Shared.Anchors;

/// <summary>One anchor, with one cell per date of the requested range. <c>Slot</c> is morning|day|evening; <c>Days</c> is the current weekday mask (Sunday = 1 ... Saturday = 64).</summary>
public sealed record AnchorDto(string Id, string Title, string Slot, int Order, string? TargetTime, int Days, bool Retired, List<AnchorPeriodDto> Periods, string Source, bool Editable, bool Writable, int Streak, List<AnchorCellDto> Cells);
