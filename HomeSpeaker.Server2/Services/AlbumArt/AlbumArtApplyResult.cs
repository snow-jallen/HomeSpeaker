namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>What Apply did: songs that got the picture, songs skipped because they already had one, and per-file failures.</summary>
public sealed record AlbumArtApplyResult(
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> AlreadyHadArt,
    IReadOnlyList<AlbumArtApplyFailure> Failures);
