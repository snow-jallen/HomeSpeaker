namespace HomeSpeaker.Server2.Services.Photos;

public sealed record PhotoEntry
{
    public required string AssetId { get; init; }
    public DateTime TakenAt { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Country { get; init; }

    /// <summary>Dimensions of the image the face boxes were measured on; 0 when there are no faces.</summary>
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public IReadOnlyList<FaceBox> Faces { get; init; } = [];

    /// <summary>Videos are listed in the manifest only; they are streamed from Immich on demand and never cached.</summary>
    public bool IsVideo { get; init; }
    public int? DurationMs { get; init; }
}
