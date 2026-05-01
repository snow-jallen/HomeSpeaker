namespace HomeSpeaker.Shared;

public record AiPlaylistDto
{
    public string GenreKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<Song> Songs { get; init; } = Array.Empty<Song>();
}
