namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// Songs that one picture should cover: every artless song of an album (same artist and album, ignoring case),
/// or a single song that has no album.
/// </summary>
public sealed record AlbumArtGroup(string Key, string Artist, string? Album, IReadOnlyList<AlbumArtSong> Songs)
{
    /// <summary>The artist <see cref="DefaultTagParser"/> uses when a file has none.</summary>
    public const string UnknownArtist = "[Artist Unknown]";

    public bool IsAlbum => !string.IsNullOrWhiteSpace(Album);

    /// <summary>The album name, or the song title when the group is a single song without an album.</summary>
    public string DisplayName => IsAlbum ? Album! : Songs[0].Name;

    public AlbumArtQuery Query => new(
        string.IsNullOrWhiteSpace(Artist) || string.Equals(Artist, UnknownArtist, StringComparison.Ordinal) ? null : Artist.Trim(),
        IsAlbum ? Album!.Trim() : null,
        IsAlbum ? null : Songs[0].Name.Trim());

    /// <summary>The persistent identity of a group, used to remember hidden groups across rescans.</summary>
    public static string KeyFor(string artist, string? album, string path) =>
        string.IsNullOrWhiteSpace(album)
            ? "song:" + path
            : "album:" + artist.Trim().ToUpperInvariant() + "\n" + album.Trim().ToUpperInvariant();
}
