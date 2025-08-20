using System.Collections.Generic;

namespace HomeSpeaker.Shared;

public record Playlist(string Name, bool AlwaysShuffle, IEnumerable<Song> Songs);
