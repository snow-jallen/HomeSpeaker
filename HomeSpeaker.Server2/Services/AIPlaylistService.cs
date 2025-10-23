using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class AIPlaylistService
{
    private readonly MusicContext _dbContext;
    private readonly Mp3Library _mp3Library;
    private readonly ILogger<AIPlaylistService> _logger;
    private readonly IConfiguration _configuration;

    public AIPlaylistService(
        MusicContext dbContext, 
        Mp3Library mp3Library, 
        ILogger<AIPlaylistService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _mp3Library = mp3Library;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task AnalyzeMusicLibraryAsync()
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key not configured. Cannot analyze music library.");
            return;
        }

        _logger.LogInformation("Starting AI analysis of music library");

        var client = new OpenAIClient(new ApiKeyCredential(apiKey));
        var chatClient = client.GetChatClient("gpt-4o-mini");

        var songs = _mp3Library.Songs.ToList();
        _logger.LogInformation("Analyzing {count} songs", songs.Count);

        // Process songs in batches
        const int batchSize = 20;
        for (int i = 0; i < songs.Count; i += batchSize)
        {
            var batch = songs.Skip(i).Take(batchSize).ToList();
            await AnalyzeBatchAsync(chatClient, batch);
            _logger.LogInformation("Processed batch {current}/{total}", Math.Min(i + batchSize, songs.Count), songs.Count);
        }

        _logger.LogInformation("Completed AI analysis of music library");
    }

    private async Task AnalyzeBatchAsync(ChatClient chatClient, List<Shared.Song> songs)
    {
        var songList = songs.Select((s, idx) => $"{idx + 1}. \"{s.Name}\" by {s.Artist} (Album: {s.Album})").ToList();
        var prompt = $@"Analyze the following songs and categorize each into one or more genres from this list: 
- Peaceful Instrumental
- Upbeat
- Classical
- Rock
- Jazz
- Electronic
- Folk
- Worship
- Children's Music
- Holiday
- Relaxing
- Energetic
- Acoustic

Songs:
{string.Join("\n", songList)}

Return ONLY a JSON array where each element has 'index' (1-based song number) and 'genres' (array of applicable genre names). Example format:
[
  {{""index"": 1, ""genres"": [""Upbeat"", ""Rock""]}},
  {{""index"": 2, ""genres"": [""Peaceful Instrumental"", ""Relaxing""]}}
]";

        try
        {
            var completion = await chatClient.CompleteChatAsync(prompt);
            var response = completion.Value.Content[0].Text;
            
            _logger.LogDebug("OpenAI response: {response}", response);

            // Extract JSON from response (it might be wrapped in markdown code blocks)
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = response.Substring(jsonStart, jsonEnd - jsonStart);
                var results = JsonSerializer.Deserialize<List<SongGenreResult>>(jsonText, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (result.Index > 0 && result.Index <= songs.Count && result.Genres != null)
                        {
                            var song = songs[result.Index - 1];
                            await SaveSongGenresAsync(song.Path, result.Genres);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing batch of songs");
        }
    }

    private async Task SaveSongGenresAsync(string songPath, List<string> genres)
    {
        try
        {
            // Remove existing genres for this song
            var existingGenres = await _dbContext.SongGenres
                .Where(sg => sg.SongPath == songPath)
                .ToListAsync();
            
            _dbContext.SongGenres.RemoveRange(existingGenres);

            // Add new genres
            foreach (var genre in genres)
            {
                var songGenre = new SongGenre
                {
                    SongPath = songPath,
                    Genre = genre
                };
                await _dbContext.SongGenres.AddAsync(songGenre);
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving genres for song {songPath}", songPath);
        }
    }

    public async Task<IEnumerable<Shared.Playlist>> GetAIPlaylistsAsync()
    {
        var allGenres = await _dbContext.SongGenres
            .GroupBy(sg => sg.Genre)
            .Select(g => new { Genre = g.Key, SongPaths = g.Select(x => x.SongPath).ToList() })
            .ToListAsync();

        var playlists = new List<Shared.Playlist>();

        foreach (var genreGroup in allGenres)
        {
            var songs = genreGroup.SongPaths
                .Select(path => _mp3Library.Songs.FirstOrDefault(s => s.Path == path))
                .Where(s => s != null)
                .Cast<Shared.Song>()
                .ToList();

            if (songs.Any())
            {
                playlists.Add(new Shared.Playlist(genreGroup.Genre, songs));
            }
        }

        return playlists;
    }

    private class SongGenreResult
    {
        public int Index { get; set; }
        public List<string>? Genres { get; set; }
    }
}
