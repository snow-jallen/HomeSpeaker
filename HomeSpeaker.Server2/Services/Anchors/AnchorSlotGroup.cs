using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>The anchors of one slot, for a "Morning / Day / Evening" section.</summary>
public sealed record AnchorSlotGroup(string Slot, string Label, IReadOnlyList<AnchorDto> Anchors);
