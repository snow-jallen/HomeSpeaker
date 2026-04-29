using System.Text;
using System.Text.Json;

namespace HomeSpeaker.Server2.Examples;

public class HomeSpeakerRestClient
{
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient httpClient;
    private readonly string baseUrl;

    public HomeSpeakerRestClient(HttpClient httpClient, string baseUrl = "https://localhost:7072")
    {
        this.httpClient = httpClient;
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    // Song Management Examples
    public async Task<IEnumerable<Song>?> GetSongsAsync(string? folder = null)
    {
        var url = $"{baseUrl}/api/homespeaker/songs";
        if (!string.IsNullOrEmpty(folder))
        {
            url += $"?folder={Uri.EscapeDataString(folder)}";
        }

        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Song>>(json, jsonOptions);
    }

    public async Task PlaySongAsync(int songId)
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/songs/{songId}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnqueueSongAsync(int songId)
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/songs/{songId}/enqueue", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSongAsync(int songId, string name, string artist, string album)
    {
        var request = new { Name = name, Artist = artist, Album = album };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PutAsync($"{baseUrl}/api/homespeaker/songs/{songId}", content);
        response.EnsureSuccessStatusCode();
    }

    // Player Control Examples
    public async Task<PlayerStatus?> GetPlayerStatusAsync()
    {
        var response = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/player/status");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PlayerStatus>(json, jsonOptions);
    }

    public async Task PlayAsync()
    {
        var request = new { Stop = false, Play = true, ClearQueue = false, SkipToNext = false, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseAsync()
    {
        var request = new { Stop = true, Play = false, ClearQueue = false, SkipToNext = false, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetVolumeAsync(int volume)
    {
        var request = new { Stop = false, Play = false, ClearQueue = false, SkipToNext = false, SetVolume = true, VolumeLevel = volume };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task SkipToNextAsync()
    {
        var request = new { Stop = false, Play = false, ClearQueue = false, SkipToNext = true, SetVolume = false, VolumeLevel = 0 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/player/control", content);
        response.EnsureSuccessStatusCode();
    }

    // Playlist Management Examples
    public async Task<IEnumerable<Playlist>?> GetPlaylistsAsync()
    {
        var response = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/playlists");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Playlist>>(json, jsonOptions);
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddSongToPlaylistAsync(string playlistName, string songPath)
    {
        var request = new { SongPath = songPath };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/songs", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveSongFromPlaylistAsync(string playlistName, string songPath)
    {
        var request = new { SongPath = songPath };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/api/homespeaker/playlists/{Uri.EscapeDataString(playlistName)}/songs")
        {
            Content = content
        };

        var response = await httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    // Queue Management Examples
    public async Task<IEnumerable<Song>?> GetQueueAsync()
    {
        var response = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/queue");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<Song>>(json, jsonOptions);
    }

    public async Task ShuffleQueueAsync()
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/queue/shuffle", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearQueueAsync()
    {
        var response = await httpClient.DeleteAsync($"{baseUrl}/api/homespeaker/queue");
        response.EnsureSuccessStatusCode();
    }

    // YouTube Integration Examples
    public async Task<IEnumerable<VideoDto>?> SearchYouTubeAsync(string searchTerm)
    {
        var response = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/youtube/search?q={Uri.EscapeDataString(searchTerm)}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<VideoDto>>(json, jsonOptions);
    }

    public async Task CacheYouTubeVideoAsync(VideoDto video)
    {
        var videoRequest = new
        {
            Video = new
            {
                video.Title,
                video.Id,
                video.Url,
                video.Thumbnail,
                video.Author,
                video.Duration
            }
        };

        var json = JsonSerializer.Serialize(videoRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/youtube/cache", content);
        response.EnsureSuccessStatusCode();
    }

    // Folder Operations Examples
    public async Task PlayFolderAsync(string folderPath)
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/folders/{Uri.EscapeDataString(folderPath)}/play", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnqueueFolderAsync(string folderPath)
    {
        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/folders/{Uri.EscapeDataString(folderPath)}/enqueue", null);
        response.EnsureSuccessStatusCode();
    }

    // Stream Operations Examples
    public async Task PlayStreamAsync(string streamUrl)
    {
        var request = new { StreamUrl = streamUrl };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/api/homespeaker/stream/play", content);
        response.EnsureSuccessStatusCode();
    }
}

// Example usage
public static class HomeSpeakerClientExample
{
    public static async Task RunExamplesAsync()
    {
        using var httpClient = new HttpClient();
        var client = new HomeSpeakerRestClient(httpClient);

        try
        {
            var songs = await client.GetSongsAsync();
            Console.WriteLine($"Found {songs?.Count()} songs in library");

            var folderSongs = await client.GetSongsAsync("Rock");
            Console.WriteLine($"Found {folderSongs?.Count()} rock songs");

            if (songs?.Any() == true)
            {
                var firstSong = songs.First();
                await client.PlaySongAsync(firstSong.SongId);
                Console.WriteLine($"Playing: {firstSong.Name}");
            }

            var status = await client.GetPlayerStatusAsync();
            Console.WriteLine($"Player status: {(status?.StillPlaying == true ? "Playing" : "Stopped")}");

            await client.SetVolumeAsync(75);
            Console.WriteLine("Volume set to 75%");

            var playlists = await client.GetPlaylistsAsync();
            Console.WriteLine($"Found {playlists?.Count()} playlists");

            var videos = await client.SearchYouTubeAsync("relaxing music");
            Console.WriteLine($"Found {videos?.Count()} YouTube videos");

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
