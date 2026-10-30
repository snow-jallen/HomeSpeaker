namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>A library song with no embedded picture. Keyed by <see cref="Path"/>: song ids change on every rescan.</summary>
public sealed record AlbumArtSong(string Path, string Name);
