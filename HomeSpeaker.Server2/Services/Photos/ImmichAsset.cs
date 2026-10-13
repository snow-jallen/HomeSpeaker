namespace HomeSpeaker.Server2.Services.Photos;

public sealed record ImmichAsset(
    string Id,
    DateTime TakenAt,
    string? City,
    string? State,
    string? Country,
    bool IsVideo = false,
    int? DurationMs = null);
