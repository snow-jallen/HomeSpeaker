using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.PlaylistSuggestions;

/// <summary>
/// Reads and decides the songs suggested for playlists (the <c>PlaylistSuggestions</c> table, filled by
/// whoever curated them). Approving a song adds it to the real playlist of the same name straight away,
/// so a playlist is usable while it's still being reviewed; un-approving takes it back out.
/// </summary>
public sealed class PlaylistSuggestionService
{
    private readonly MusicContext db;
    private readonly Mp3Library library;
    private readonly PlaylistService playlists;
    private readonly TimeProvider timeProvider;

    public PlaylistSuggestionService(MusicContext db, Mp3Library library, PlaylistService playlists, TimeProvider timeProvider)
    {
        this.db = db;
        this.library = library;
        this.playlists = playlists;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SuggestionPlaylistSummary>> GetPlaylistsAsync()
    {
        var counts = await db.PlaylistSuggestions
            .AsNoTracking()
            .GroupBy(s => new { s.PlaylistName, s.Status })
            .Select(g => new { g.Key.PlaylistName, g.Key.Status, Count = g.Count(), FirstOrder = g.Min(s => s.Id) })
            .ToListAsync();

        return counts
            .GroupBy(c => c.PlaylistName)
            .OrderBy(g => g.Min(c => c.FirstOrder))
            .Select(g => new SuggestionPlaylistSummary(
                g.Key,
                Pending: g.Where(c => c.Status == SuggestionStatus.Pending).Sum(c => c.Count),
                Approved: g.Where(c => c.Status == SuggestionStatus.Approved).Sum(c => c.Count),
                Rejected: g.Where(c => c.Status == SuggestionStatus.Rejected).Sum(c => c.Count)))
            .ToList();
    }

    /// <summary>The playlist's suggestions in review order. Songs no longer in the library are left out.</summary>
    public async Task<IReadOnlyList<SuggestedSong>> GetSongsAsync(string playlistName)
    {
        var rows = await db.PlaylistSuggestions
            .AsNoTracking()
            .Where(s => s.PlaylistName == playlistName)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();
        var songsByPath = library.Songs
            .Where(s => s.Path is not null)
            .GroupBy(s => s.Path!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return rows
            .Select(row => songsByPath.TryGetValue(row.SongPath, out var song)
                ? new SuggestedSong(row.Id, row.Status, row.Reason, song.SongId, song.Name, song.Artist, song.Album, row.SongPath)
                : null)
            .OfType<SuggestedSong>()
            .ToList();
    }

    /// <summary>Sets one suggestion's status, adding the song to or removing it from the playlist to match.</summary>
    public async Task DecideAsync(int suggestionId, SuggestionStatus status)
    {
        var suggestion = await db.PlaylistSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId);
        if (suggestion is null || suggestion.Status == status)
        {
            return;
        }

        await applyAsync(suggestion, status);
    }

    /// <summary>Approves every suggestion still pending for the playlist; returns how many.</summary>
    public async Task<int> ApproveRemainingAsync(string playlistName)
    {
        var pending = await db.PlaylistSuggestions
            .Where(s => s.PlaylistName == playlistName && s.Status == SuggestionStatus.Pending)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();
        foreach (var suggestion in pending)
        {
            await applyAsync(suggestion, SuggestionStatus.Approved);
        }

        return pending.Count;
    }

    private async Task applyAsync(PlaylistSuggestion suggestion, SuggestionStatus status)
    {
        var wasApproved = suggestion.Status == SuggestionStatus.Approved;
        suggestion.Status = status;
        suggestion.ReviewedUtc = status == SuggestionStatus.Pending ? null : timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        if (status == SuggestionStatus.Approved && !wasApproved && !await isInPlaylistAsync(suggestion))
        {
            await playlists.AppendSongToPlaylistAsync(suggestion.PlaylistName, suggestion.SongPath);
        }
        else if (wasApproved && status != SuggestionStatus.Approved)
        {
            await playlists.RemoveSongFromPlaylistAsync(suggestion.PlaylistName, suggestion.SongPath);
        }
    }

    private Task<bool> isInPlaylistAsync(PlaylistSuggestion suggestion) =>
        db.PlaylistItems.AnyAsync(i => i.SongPath == suggestion.SongPath
            && db.Playlists.Any(p => p.Id == i.PlaylistId && p.Name == suggestion.PlaylistName));
}
