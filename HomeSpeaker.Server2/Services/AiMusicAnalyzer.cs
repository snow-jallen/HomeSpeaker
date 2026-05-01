using System.Text;
using System.Text.Json;
using HomeSpeaker.Server2.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class AiMusicAnalyzer
{
    private readonly IChatClient chatClient;
    private readonly AiMusicOptions options;
    private readonly ILogger<AiMusicAnalyzer> logger;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AiMusicAnalyzer(IChatClient chatClient, IOptions<AiMusicOptions> options, ILogger<AiMusicAnalyzer> logger)
    {
        this.chatClient = chatClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<AiTrackAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<AiSongCandidate> songs,
        IReadOnlyList<AiGenreDefinition> genres,
        CancellationToken cancellationToken)
    {
        if (songs.Count == 0)
        {
            return Array.Empty<AiTrackAnalysisResult>();
        }

        var prompt = buildPrompt(songs, genres);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a music metadata classifier. Always respond with strict JSON only."),
            new(ChatRole.User, prompt)
        };

        var chatOptions = new ChatOptions
        {
            Temperature = 0.2f,
            MaxOutputTokens = 2000,
            ResponseFormat = ChatResponseFormat.Json,
            ModelId = options.ConfiguredModelId
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            logger.LogWarning("AI response was empty for {Count} songs", songs.Count);
            return Array.Empty<AiTrackAnalysisResult>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AiBatchAnalysisResponse>(response.Text, jsonOptions);
            return payload?.Songs ?? new List<AiTrackAnalysisResult>();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse AI response JSON");
            throw;
        }
    }

    private static string buildPrompt(IReadOnlyList<AiSongCandidate> songs, IReadOnlyList<AiGenreDefinition> genres)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following songs. Return JSON with this schema:");
        sb.AppendLine("{\"songs\":[{\"songPath\":\"\",\"summary\":\"\",\"tempoLabel\":\"\",\"primaryMood\":\"\",\"energy\":0.0,\"acousticness\":0.0,\"instrumentalness\":0.0,\"vocalPresence\":0.0,\"sacredness\":0.0,\"seasonalityChristmas\":0.0,\"danceability\":0.0,\"warmth\":0.0,\"confidence\":0.0,\"markers\":[{\"key\":\"\",\"value\":0.0,\"confidence\":0.0}],\"genres\":[{\"genreKey\":\"\",\"score\":0.0,\"rank\":0,\"why\":\"\"}]}]}");
        sb.AppendLine("Use only these genre keys:");
        foreach (var genre in genres.OrderBy(g => g.SortOrder))
        {
            sb.AppendLine($"- {genre.Key}");
        }

        sb.AppendLine("Songs:");
        foreach (var song in songs)
        {
            sb.AppendLine($"- songPath: {song.SongPath}");
            sb.AppendLine($"  title: {song.Title}");
            sb.AppendLine($"  artist: {song.Artist}");
            sb.AppendLine($"  album: {song.Album}");
            if (!string.IsNullOrWhiteSpace(song.FolderHint))
            {
                sb.AppendLine($"  folderHint: {song.FolderHint}");
            }
        }

        sb.AppendLine("Normalize numeric scores to 0..1.");
        sb.AppendLine("Provide at least 3 markers per song.");
        return sb.ToString();
    }
}

public sealed record AiSongCandidate(string SongPath, string Title, string Artist, string Album, string? FolderHint);

public sealed class AiBatchAnalysisResponse
{
    public List<AiTrackAnalysisResult> Songs { get; set; } = new();
}

public sealed class AiTrackAnalysisResult
{
    public string SongPath { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string TempoLabel { get; set; } = string.Empty;
    public string PrimaryMood { get; set; } = string.Empty;
    public double Energy { get; set; }
    public double Acousticness { get; set; }
    public double Instrumentalness { get; set; }
    public double VocalPresence { get; set; }
    public double Sacredness { get; set; }
    public double SeasonalityChristmas { get; set; }
    public double Danceability { get; set; }
    public double Warmth { get; set; }
    public double Confidence { get; set; }
    public List<AiMarkerResult> Markers { get; set; } = new();
    public List<AiGenreScoreResult> Genres { get; set; } = new();
}

public sealed class AiMarkerResult
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Confidence { get; set; }
}

public sealed class AiGenreScoreResult
{
    public string GenreKey { get; set; } = string.Empty;
    public double Score { get; set; }
    public int Rank { get; set; }
    public string? Why { get; set; }
}
