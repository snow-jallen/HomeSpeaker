using System.Collections.Immutable;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.PlayingPlaylist;

/// <summary>
/// Remembers which playlist the speaker is working through, so the UI can offer to take the playing song out
/// of it. <see cref="PlaylistService.PlayPlaylistAsync"/> starts it; clearing the queue or playing a stream
/// (through <see cref="ChattyMusicPlayer"/>) ends it. A song counts only if it came from that playlist, so a
/// song played or queued by hand in between doesn't offer the button. <see cref="LifecycleEvents"/> saves it
/// with the rest of the playback state, so a restart that resumes the queue resumes the playlist too.
/// </summary>
public sealed class PlayingPlaylistTracker
{
    private volatile PlayingPlaylistState? current;

    public void Started(string playlistName, IEnumerable<string> songPaths) =>
        current = new PlayingPlaylistState(playlistName, songPaths.ToImmutableHashSet(StringComparer.Ordinal));

    public void Forget() => current = null;

    /// <summary>The playlist being played and the songs that count as coming from it, or null.</summary>
    public PlayingPlaylistState? Current => current;

    /// <summary>The playlist <paramref name="song"/> is being played from, or null.</summary>
    public string? PlaylistFor(Song? song) =>
        current is { } state && song?.Path is { } path && state.SongPaths.Contains(path) ? state.PlaylistName : null;

    /// <summary>The song left the playlist; it no longer counts as playing from it.</summary>
    public void Removed(string songPath)
    {
        if (current is { } state)
        {
            current = state with { SongPaths = state.SongPaths.Remove(songPath) };
        }
    }
}
