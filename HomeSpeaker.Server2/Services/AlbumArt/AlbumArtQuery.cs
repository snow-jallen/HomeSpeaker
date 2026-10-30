namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// What to look up: an album (<see cref="Album"/> set), a single song (<see cref="Title"/> set), or the admin's own
/// words (<see cref="Text"/>, from <see cref="FreeText"/>), which are searched as albums. <see cref="Artist"/> is null when unknown.
/// </summary>
public sealed record AlbumArtQuery(string? Artist, string? Album, string? Title)
{
    /// <summary>Typed by the admin, for when the tags don't find the right cover. Takes the place of the other fields.</summary>
    public string? Text { get; private init; }

    public bool IsFreeText => !string.IsNullOrWhiteSpace(Text);

    public bool IsAlbum => IsFreeText || !string.IsNullOrWhiteSpace(Album);

    /// <summary>The typed text, the album name for album lookups, otherwise the song title.</summary>
    public string Name => (IsFreeText ? Text : IsAlbum ? Album : Title) ?? string.Empty;

    public static AlbumArtQuery FreeText(string text) => new(null, null, null) { Text = text.Trim() };
}
