namespace HomeSpeaker.Shared;

public record Playlist(string Name, bool AlwaysShuffle, IEnumerable<Song> Songs);
