using System.Collections.Immutable;

namespace HomeSpeaker.Server2.Services.PlayingPlaylist;

public sealed record PlayingPlaylistState(string PlaylistName, ImmutableHashSet<string> SongPaths);
