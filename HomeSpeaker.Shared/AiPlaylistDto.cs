namespace HomeSpeaker.Shared;

public record AiPlaylistDto
{
    public string GenreKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<AiPlaylistTrackDto> Tracks { get; init; } = Array.Empty<AiPlaylistTrackDto>();
    public IReadOnlyList<Song> Songs { get; init; } = Array.Empty<Song>();
}

public record AiPlaylistTrackDto
{
    public required Song Song { get; init; }
    public double GenreScore { get; init; }
    public int GenreRank { get; init; }
    public string? Why { get; init; }
    public IReadOnlyList<AiPlaylistTrackMarkerDto> Markers { get; init; } = Array.Empty<AiPlaylistTrackMarkerDto>();
}

public record AiPlaylistTrackMarkerDto
{
    public string Key { get; init; } = string.Empty;
    public double Value { get; init; }
    public double Confidence { get; init; }
}
