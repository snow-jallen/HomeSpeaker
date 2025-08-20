using HomeSpeaker.Server;
using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class PlaylistService
{
    private readonly MusicContext dbContext;
    private readonly Mp3Library mp3Library;
    private readonly ILogger<PlaylistService> logger;
    private readonly IMusicPlayer player;

    public PlaylistService(MusicContext dbContext, Mp3Library mp3Library, ILogger<PlaylistService> logger, IMusicPlayer player)
    {
        this.dbContext = dbContext;
        this.mp3Library = mp3Library;
        this.logger = logger;
        this.player = player;
    }
    private Shared.Song? findSong(PlaylistItem item) => mp3Library.Songs.Where(s => s.Path == item.SongPath).FirstOrDefault();

    public async Task<IEnumerable<Shared.Playlist>> GetPlaylistsAsync()
    {
        var dbPlaylists = await dbContext.Playlists.Include(p => p.Songs).ToListAsync();
        logger.LogInformation("Found {count} playlists in database.", dbPlaylists.Count);
        return dbPlaylists.Select(p => new Shared.Playlist(p.Name, p.AlwaysShuffle, p.Songs.OrderBy(s => s.Order).Select(i => findSong(i))));
    }

    public async Task AppendSongToPlaylistAsync(string playlistName, string songPath)
    {
        logger.LogInformation("Adding {songPath} to {playlist} playlist", songPath, playlistName);

        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            playlist = new Playlist
            {
                Name = playlistName
            };
            await dbContext.Playlists.AddAsync(playlist);
            await dbContext.SaveChangesAsync();
        }
        var playlistItem = new PlaylistItem
        {
            PlaylistId = playlist.Id,
            SongPath = songPath,
            Order = playlist.Songs.Count
        };
        await dbContext.PlaylistItems.AddAsync(playlistItem);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveSongFromPlaylistAsync(string playlistName, string songPath)
    {
        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("User tried to remove {song} from {playlistName} but that playlist doesn't exist.", songPath, playlistName);
            return;
        }
        var playlistItem = await dbContext.PlaylistItems.FirstOrDefaultAsync(i => i.PlaylistId == playlist.Id && i.SongPath == songPath);
        if (playlistItem == null)
        {
            logger.LogWarning("User tried to remove {song} from {playlistName} but that song isn't in that playlist.", songPath, playlistName);
            return;
        }

        logger.LogInformation("Removing {song} from {playlistName}", songPath, playlistName);
        dbContext.PlaylistItems.Remove(playlistItem);
        await dbContext.SaveChangesAsync();
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        var playlist = await dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to play playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        logger.LogInformation("Beginning to play playlist {playlistName}", playlistName);

        player.Stop();

        IEnumerable<PlaylistItem> songsToPlay;
        if (playlist.AlwaysShuffle)
        {
            logger.LogInformation("Playlist {playlistName} has AlwaysShuffle enabled, shuffling before playing", playlistName);
            await ShufflePlaylistAsync(playlistName);
            // Reload playlist to get shuffled order
            playlist = await dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
            songsToPlay = playlist!.Songs.OrderBy(s => s.Order);
        }
        else
        {
            songsToPlay = playlist.Songs.OrderBy(s => s.Order);
        }

        foreach (var playlistItem in songsToPlay)
        {
            var song = mp3Library.Songs.Single(s => s.Path == playlistItem.SongPath);
            player.EnqueueSong(song);
        }
    }

    public async Task RenamePlaylistAsync(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            logger.LogWarning("Attempted to rename playlist {oldName} to an empty name.", oldName);
            return;
        }

        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == oldName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to rename playlist {oldName} but it doesn't exist.", oldName);
            return;
        }

        // Check if a playlist with the new name already exists
        var existingPlaylist = await dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == newName);
        if (existingPlaylist != null)
        {
            logger.LogWarning("Cannot rename playlist {oldName} to {newName} because a playlist with that name already exists.", oldName, newName);
            return;
        }

        logger.LogInformation("Renaming playlist from {oldName} to {newName}", oldName, newName);
        playlist.Name = newName;
        await dbContext.SaveChangesAsync();
    }

    public async Task DeletePlaylistAsync(string playlistName)
    {
        var playlist = await dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to delete playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        logger.LogInformation("Deleting playlist {playlistName} with {songCount} songs", playlistName, playlist.Songs.Count);
        
        // Remove all songs from the playlist first
        dbContext.PlaylistItems.RemoveRange(playlist.Songs);
        
        // Remove the playlist itself
        dbContext.Playlists.Remove(playlist);
        
        await dbContext.SaveChangesAsync();
    }

    public async Task ReorderPlaylistSongsAsync(string playlistName, IEnumerable<string> songPathsInNewOrder)
    {
        var playlist = await dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to reorder songs in playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        var songPathsList = songPathsInNewOrder.ToList();
        logger.LogInformation("Reordering {songCount} songs in playlist {playlistName}", songPathsList.Count, playlistName);

        // Update the order of existing songs
        for (int i = 0; i < songPathsList.Count; i++)
        {
            var songPath = songPathsList[i];
            var playlistItem = playlist.Songs.FirstOrDefault(s => s.SongPath == songPath);
            if (playlistItem != null)
            {
                playlistItem.Order = i;
            }
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Successfully reordered songs in playlist {playlistName}", playlistName);
    }

    public async Task SetPlaylistAlwaysShuffleAsync(string playlistName, bool alwaysShuffle)
    {
        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to set AlwaysShuffle for playlist {playlistName} but it doesn't exist.", playlistName);
            return;
        }

        logger.LogInformation("Setting AlwaysShuffle to {alwaysShuffle} for playlist {playlistName}", alwaysShuffle, playlistName);
        playlist.AlwaysShuffle = alwaysShuffle;
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<string>> ShufflePlaylistAsync(string playlistName)
    {
        var playlist = await dbContext.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Name == playlistName);
        if (playlist == null)
        {
            logger.LogWarning("Asked to shuffle playlist {playlistName} but it doesn't exist.", playlistName);
            return Enumerable.Empty<string>();
        }

        if (playlist.Songs.Count <= 1)
        {
            logger.LogInformation("Playlist {playlistName} has {count} songs, no need to shuffle", playlistName, playlist.Songs.Count);
            return playlist.Songs.Select(s => s.SongPath).ToList();
        }

        // Get songs with their artist information for smart shuffling
        var songsWithArtists = playlist.Songs.Select(item =>
        {
            var song = mp3Library.Songs.FirstOrDefault(s => s.Path == item.SongPath);
            return new { Item = item, Artist = song?.Artist ?? "Unknown" };
        }).ToList();

        // Implement artist-aware shuffling
        var shuffledSongs = ShuffleWithArtistDistribution(songsWithArtists);
        
        // Update the order in the database
        for (int i = 0; i < shuffledSongs.Count; i++)
        {
            shuffledSongs[i].Item.Order = i;
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Successfully shuffled {count} songs in playlist {playlistName}", shuffledSongs.Count, playlistName);
        
        return shuffledSongs.Select(s => s.Item.SongPath).ToList();
    }

    private List<T> ShuffleWithArtistDistribution<T>(List<T> items) where T : class
    {
        if (items.Count <= 1) return items;

        var random = new Random();
        var result = new List<T>();
        var remaining = new List<T>(items);

        // Helper function to get artist for an item
        string GetArtist(T item)
        {
            var artistProp = item.GetType().GetProperty("Artist");
            return artistProp?.GetValue(item)?.ToString() ?? "Unknown";
        }

        // Start with a random song
        var firstSong = remaining[random.Next(remaining.Count)];
        result.Add(firstSong);
        remaining.Remove(firstSong);

        while (remaining.Count > 0)
        {
            var lastArtist = GetArtist(result.Last());
            
            // Try to find a song from a different artist
            var differentArtistSongs = remaining.Where(s => GetArtist(s) != lastArtist).ToList();
            
            T nextSong;
            if (differentArtistSongs.Any())
            {
                // Pick randomly from songs with different artists
                nextSong = differentArtistSongs[random.Next(differentArtistSongs.Count)];
            }
            else
            {
                // If all remaining songs are from the same artist, pick randomly
                nextSong = remaining[random.Next(remaining.Count)];
            }
            
            result.Add(nextSong);
            remaining.Remove(nextSong);
        }

        return result;
    }
}