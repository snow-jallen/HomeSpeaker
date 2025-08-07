using System.Text.Json;
using System.Text;

namespace HomeSpeaker.Client.Examples;

/// <summary>
/// Example client demonstrating how to use the HomeSpeaker REST API endpoints
/// </summary>
public class HomeSpeakerRestClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HomeSpeakerRestClient(HttpClient httpClient, string baseUrl = "https://localhost:7072")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // Song Management Examples
    public async Task<IEnumerable<Song>?> GetSongsAsync(string? folder = null)
    {
        var url = $"{_baseUrl}/api/homespeaker/songs";
        if (!string.IsNullOrEmpty(folder))
        {
            url += $"?folder={Uri.EscapeDataString(folder)}";
        }
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Song>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task PlaySongAsync(int songId)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/songs/{songId}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnqueueSongAsync(int songId)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/songs/{songId}/enqueue", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSongAsync(int songId, string name, string artist, string album)
    {
        var request = new { Name = name, Artist = artist, Album = album };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PutAsync($"{_baseUrl}/api/homespeaker/songs/{songId}", content);
        response.EnsureSuccessStatusCode();
    }

    // Player Control Examples
    public async Task<PlayerStatus?> GetPlayerStatusAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/homespeaker/player/status");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PlayerStatus>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task PlayAsync()
    {
        var request = new { Stop = false, Play = true, ClearQueue = false, SkipToNext = false, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseAsync()
    {
        var request = new { Stop = true, Play = false, ClearQueue = false, SkipToNext = false, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetVolumeAsync(int volume)
    {
        var request = new { Stop = false, Play = false, ClearQueue = false, SkipToNext = false, SetVolume = true, VolumeLevel = volume };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task SkipToNextAsync()
    {
        var request = new { Stop = false, Play = false, ClearQueue = false, SkipToNext = true, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    // Playlist Management Examples
    public async Task<IEnumerable<Playlist>?> GetPlaylistsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/homespeaker/playlists");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Playlist>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddSongToPlaylistAsync(string playlistName, string songPath)
    {
        var request = new { SongPath = songPath };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/songs", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveSongFromPlaylistAsync(string playlistName, string songPath)
    {
        var request = new { SongPath = songPath };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/songs")
        {
            Content = content
        };
        
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    // Queue Management Examples
    public async Task<IEnumerable<Song>?> GetQueueAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/homespeaker/queue");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Song>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task ShuffleQueueAsync()
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/queue/shuffle", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearQueueAsync()
    {
        var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/homespeaker/queue");
        response.EnsureSuccessStatusCode();
    }

    // YouTube Integration Examples
    public async Task<IEnumerable<VideoDto>?> SearchYouTubeAsync(string searchTerm)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/homespeaker/youtube/search?q={Uri.EscapeDataString(searchTerm)}");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<VideoDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task CacheYouTubeVideoAsync(VideoDto video)
    {
        // Convert VideoDto to the Video format expected by the API
        var videoRequest = new 
        {
            Video = new 
            {
                Title = video.Title,
                Id = video.Id,
                Url = video.Url,
                Thumbnail = video.Thumbnail,
                Author = video.Author,
                Duration = video.Duration
            }
        };
        
        var json = JsonSerializer.Serialize(videoRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/youtube/cache", content);
        response.EnsureSuccessStatusCode();
    }

    // Folder Operations Examples
    public async Task PlayFolderAsync(string folderPath)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/folders/{Uri.EscapeDataString(folderPath)}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnqueueFolderAsync(string folderPath)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/folders/{Uri.EscapeDataString(folderPath)}/enqueue", null);
        response.EnsureSuccessStatusCode();
    }

    // Stream Operations Examples
    public async Task PlayStreamAsync(string streamUrl)
    {
        var request = new { StreamUrl = streamUrl };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/homespeaker/stream/play", content);
        response.EnsureSuccessStatusCode();
    }
}

// Example usage
public class HomeSpeakerClientExample
{
    public static async Task RunExamplesAsync()
    {
        using var httpClient = new HttpClient();
        var client = new HomeSpeakerRestClient(httpClient);

        try
        {
            // Get all songs
            var songs = await client.GetSongsAsync();
            Console.WriteLine($"Found {songs?.Count()} songs in library");

            // Get songs from a specific folder
            var folderSongs = await client.GetSongsAsync("Rock");
            Console.WriteLine($"Found {folderSongs?.Count()} rock songs");

            // Play the first song
            if (songs?.Any() == true)
            {
                var firstSong = songs.First();
                await client.PlaySongAsync(firstSong.SongId);
                Console.WriteLine($"Playing: {firstSong.Name}");
            }

            // Get player status
            var status = await client.GetPlayerStatusAsync();
            Console.WriteLine($"Player status: {(status?.StillPlaying == true ? "Playing" : "Stopped")}");

            // Control playback
            await client.SetVolumeAsync(75);
            Console.WriteLine("Volume set to 75%");

            // Get playlists
            var playlists = await client.GetPlaylistsAsync();
            Console.WriteLine($"Found {playlists?.Count()} playlists");

            // Search YouTube
            var videos = await client.SearchYouTubeAsync("relaxing music");
            Console.WriteLine($"Found {videos?.Count()} YouTube videos");

            // Get current queue
            var queue = await client.GetQueueAsync();
            Console.WriteLine($"Queue has {queue?.Count()} songs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

// Simple data models for the examples
public class Song
{
    public int SongId { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Album { get; set; } = "";
    public string Artist { get; set; } = "";
}

public class Playlist
{
    public string PlaylistName { get; set; } = "";
    public IEnumerable<Song> Songs { get; set; } = new List<Song>();
}

public class PlayerStatus
{
    public TimeSpan Elapsed { get; set; }
    public TimeSpan Remaining { get; set; }
    public bool StillPlaying { get; set; }
    public double PercentComplete { get; set; }
    public Song? CurrentSong { get; set; }
    public int Volume { get; set; }
}

public class VideoDto
{
    public string Title { get; set; } = "";
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Thumbnail { get; set; }
    public string? Author { get; set; }
    public TimeSpan? Duration { get; set; }
}