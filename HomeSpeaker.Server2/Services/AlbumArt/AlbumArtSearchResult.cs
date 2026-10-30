namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Candidates from every source that answered, best match first, plus the names of sources that failed.</summary>
public sealed record AlbumArtSearchResult(IReadOnlyList<AlbumArtCandidate> Candidates, IReadOnlyList<string> FailedSources);
