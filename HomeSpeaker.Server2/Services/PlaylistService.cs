using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class PlaylistService
{
    private readonly MusicContext _dbContext;
    private readonly Mp3Library _mp3Library;
    private readonly ILogger<PlaylistService> _logger;
    private readonly IMusicPlayer _player;

    public PlaylistService(MusicContext dbContext, Mp3Library mp3Library, ILogger<PlaylistService> logger, IMusicPlayer player)
    {
        _dbContext = dbContext;
        _mp3Library = mp3Library;
        _logger = logger;
        _player = player;
    }
    private Shared.Song? findSong(PlaylistItem item) => _mp3Library.Songs.Where(s => s.Path == item.SongPath).FirstOrDefault();

    public async Task<IEnumerable<Shared.Playlist>> GetPlaylistsAsync()
    {
        var dbPlaylists = await _dbContext.Playlists.Include(p => p.Songs).AsNoTracking().ToListAsync();
        _logger.LogInformation("Found {count} playlists in database.", dbPlaylists.Count);

        // Performance: Build dictionary once instead of O(n) lookup for each song
        var songsByPath = _mp3Library.Songs.ToDictionary(s => s.Path, s => s);

        return dbPlaylists.Select(p => new Shared.Playlist(
            p.Name,
            p.Songs.OrderBy(s => s.Order)
                   .Select(i => songsByPath.GetValueOrDefault(i.SongPath))
        ));
    }

    public async Task AppendSongToPlaylistAsync(string playlistName, string songPath)
    {
        _logger.LogInformation("Adding {songPath} to {playlist} playlist", songPath, playlistName);

        var playlist = await _dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            playlist = new Playlist
            {
                Name = playlistName
            };
            await _dbContext.Playlists.AddAsync(playlist);
            await _dbContext.SaveChangesAsync();
        }
        var playlistItem = new PlaylistItem
        {
            PlaylistId = playlist.Id,
            SongPath = songPath,
            Order = playlist.Songs.Count
        };
        await _dbContext.PlaylistItems.AddAsync(playlistItem);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveSongFromPlaylistAsync(string playlistName, string songPath)
    {
        var playlist = await _dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            _logger.LogWarning("User tried to remove {song} from {playlistName} but that playlist doesn't exist.", songPath, playlistName);
            return;
        }
        var playlistItem = await _dbContext.PlaylistItems.FirstOrDefaultAsync(i => i.PlaylistId == playlist.Id && i.SongPath == songPath);
        if (playlistItem == null)
        {
            _logger.LogWarning("User tried to remove {song} from {playlistName} but that song isn't in that playlist.", songPath, playlistName);
            return;
        }

        _logger.LogInformation("Removing {song} from {playlistName}", songPath, playlistName);
        _dbContext.PlaylistItems.Remove(playlistItem);
        await _dbContext.SaveChangesAsync();
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        var playlist = await _dbContext.Playlists.Include(p => p.Songs).AsNoTracking().FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            _logger.LogWarning("Asked to play playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        _logger.LogInformation("Beginning to play playlist {playlistName}", playlistName);

        // Performance: Build dictionary once instead of O(n) lookup for each song
        var songsByPath = _mp3Library.Songs.ToDictionary(s => s.Path, s => s);

        _player.Stop();
        foreach (var playlistItem in playlist.Songs.OrderBy(s => s.Order))
        {
            if (songsByPath.TryGetValue(playlistItem.SongPath, out var song))
            {
                _player.EnqueueSong(song);
            }
        }
    }

    public async Task RenamePlaylistAsync(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            _logger.LogWarning("Attempted to rename playlist {oldName} to an empty name.", oldName);
            return;
        }

        var playlist = await _dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == oldName);
        if (playlist == null)
        {
            _logger.LogWarning("Asked to rename playlist {oldName} but it doesn't exist.", oldName);
            return;
        }

        // Check if a playlist with the new name already exists
        var existingPlaylist = await _dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == newName);
        if (existingPlaylist != null)
        {
            _logger.LogWarning("Cannot rename playlist {oldName} to {newName} because a playlist with that name already exists.", oldName, newName);
            return;
        }

        _logger.LogInformation("Renaming playlist from {oldName} to {newName}", oldName, newName);
        playlist.Name = newName;
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeletePlaylistAsync(string playlistName)
    {
        var playlist = await _dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            _logger.LogWarning("Asked to delete playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        _logger.LogInformation("Deleting playlist {playlistName} with {songCount} songs", playlistName, playlist.Songs.Count);
        
        // Remove all songs from the playlist first
        _dbContext.PlaylistItems.RemoveRange(playlist.Songs);
        
        // Remove the playlist itself
        _dbContext.Playlists.Remove(playlist);
        
        await _dbContext.SaveChangesAsync();
    }

    public async Task ReorderPlaylistSongsAsync(string playlistName, IEnumerable<string> songPathsInNewOrder)
    {
        var playlist = await _dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            _logger.LogWarning("Asked to reorder songs in playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        var songPathsList = songPathsInNewOrder.ToList();
        _logger.LogInformation("Reordering {songCount} songs in playlist {playlistName}", songPathsList.Count, playlistName);

        // Performance: Build dictionary for O(1) lookup instead of O(n²) nested loop
        var songsByPath = playlist.Songs.ToDictionary(s => s.SongPath, s => s);

        for (int i = 0; i < songPathsList.Count; i++)
        {
            var songPath = songPathsList[i];
            if (songsByPath.TryGetValue(songPath, out var playlistItem))
            {
                playlistItem.Order = i;
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Successfully reordered songs in playlist {playlistName}", playlistName);
    }
}