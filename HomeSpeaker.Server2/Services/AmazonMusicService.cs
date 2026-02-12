using HomeSpeaker.Server2;

namespace HomeSpeaker.Server2.Services;

public class AmazonMusicService
{
    private readonly ILogger<AmazonMusicService> _logger;
    private readonly IMusicPlayer _player;
    private readonly Mp3Library _mp3Library;

    public AmazonMusicService(ILogger<AmazonMusicService> logger, IMusicPlayer player, Mp3Library mp3Library)
    {
        _logger = logger;
        _player = player;
        _mp3Library = mp3Library;
    }

    public async Task<IEnumerable<AmazonPlaylist>> GetAmazonPlaylistsAsync()
    {
        // TODO: In the future, this will call the actual Amazon Music API
        // For now, return mock playlists to demonstrate the feature
        _logger.LogInformation("Fetching Amazon Music playlists (currently mock data)");
        
        await Task.CompletedTask; // Simulate async call
        
        return new List<AmazonPlaylist>
        {
            new AmazonPlaylist("playlist-1", "My Favorites", 25),
            new AmazonPlaylist("playlist-2", "Workout Mix", 30),
            new AmazonPlaylist("playlist-3", "Chill Vibes", 40),
            new AmazonPlaylist("playlist-4", "Rock Classics", 50),
            new AmazonPlaylist("playlist-5", "Jazz Collection", 35)
        };
    }

    public async Task PlayAmazonPlaylistAsync(string playlistId)
    {
        _logger.LogInformation("Playing Amazon Music playlist: {PlaylistId}", playlistId);
        
        // TODO: In the future, this will:
        // 1. Authenticate with Amazon Music API
        // 2. Fetch the playlist tracks
        // 3. Download or stream the tracks
        // For now, we'll shuffle and play random songs from the local library as a placeholder
        
        await Task.CompletedTask; // Simulate async call
        
        _player.Stop();
        
        // Get a random selection of songs from the library
        var random = new Random();
        var songsToPlay = _mp3Library.Songs
            .OrderBy(x => random.Next())
            .Take(10)
            .ToList();
        
        foreach (var song in songsToPlay)
        {
            _player.EnqueueSong(song);
        }
        
        _logger.LogInformation("Enqueued {Count} songs for Amazon playlist {PlaylistId}", songsToPlay.Count, playlistId);
    }
}

public record AmazonPlaylist(string PlaylistId, string PlaylistName, int TrackCount);
