namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>What to look up: an album (<see cref="Album"/> set) or a single song (<see cref="Title"/> set). <see cref="Artist"/> is null when unknown.</summary>
public sealed record AlbumArtQuery(string? Artist, string? Album, string? Title)
{
    public bool IsAlbum => !string.IsNullOrWhiteSpace(Album);

    /// <summary>The album name for album lookups, otherwise the song title.</summary>
    public string Name => (IsAlbum ? Album : Title) ?? string.Empty;
}
