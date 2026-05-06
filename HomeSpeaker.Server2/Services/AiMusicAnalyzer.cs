using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HomeSpeaker.Server2.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class AiMusicAnalyzer
{
    private static readonly TimeSpan minimumModelRequestTimeout = TimeSpan.FromSeconds(30);
    private const int MinimumMaxOutputTokens = 2000;
    private const int EstimatedOutputTokensPerSong = 400;
    private static readonly Regex repairableNumericFieldPattern = new(
        "(?<prefix>\"(?<field>energy|acousticness|instrumentalness|vocalPresence|sacredness|seasonalityChristmas|danceability|warmth|confidence|value|score|rank)\"\\s*:\\s*)(?<value>[+-]?(?:\\d+[\\.,]?\\d*|\\.\\d+)(?:[eE][+-]?\\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex strictJsonNumberPattern = new(
        "^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?(?:[eE][+-]?\\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
            MaxOutputTokens = Math.Max(MinimumMaxOutputTokens, songs.Count * EstimatedOutputTokensPerSong),
            ResponseFormat = ChatResponseFormat.Json,
            ModelId = options.ConfiguredModelId
        };

        var requestTimeout = TimeSpan.FromSeconds(Math.Max(
            minimumModelRequestTimeout.TotalSeconds,
            options.ModelRequestTimeoutSeconds));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(requestTimeout);

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, chatOptions, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            var message = $"AI model request timed out after {requestTimeout:mm\\:ss}.";
            logger.LogWarning(ex, "{Message} Batch size was {Count}.", message, songs.Count);
            throw new TimeoutException(message, ex);
        }

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            logger.LogWarning("AI response was empty for {Count} songs", songs.Count);
            return Array.Empty<AiTrackAnalysisResult>();
        }

        try
        {
            var allowedGenreKeys = genres
                .Where(genre => !string.IsNullOrWhiteSpace(genre.Key))
                .GroupBy(genre => genre.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);
            var payload = deserializeResponse(response.Text, allowedGenreKeys);
            return payload?.Songs ?? new List<AiTrackAnalysisResult>();
        }
        catch (JsonException ex)
        {
            var failureKind = classifyJsonFailure(response.Text, ex);
            logger.LogError(
                ex,
                "Failed to parse AI response JSON ({FailureKind}) at {Path}. Context: {Context}. Response preview: {Preview}",
                failureKind,
                ex.Path ?? "$",
                buildJsonErrorContext(response.Text, ex),
                summarizeResponse(response.Text));
            throw new AiBatchAnalysisException(
                failureKind,
                ex.Path ?? "$",
                buildJsonErrorContext(response.Text, ex),
                summarizeResponse(response.Text),
                ex);
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
        sb.AppendLine("Every numeric field must be a valid JSON number, not a string.");
        sb.AppendLine("Never use leading zero integers like 01 or 00.42; use 1 or 0.42.");
        sb.AppendLine("Use a period for decimals and include digits on both sides when needed (for example 0.4, never .4, 0., or 0,4).");
        sb.AppendLine($"Return exactly {songs.Count} song objects in the same order as the input songs.");
        sb.AppendLine("Provide exactly 3 markers per song and at most 3 genres per song.");
        sb.AppendLine("Keep summary to one sentence under 160 characters. Keep each genre why under 80 characters.");
        sb.AppendLine("If space is tight, shorten text fields. Never truncate the JSON document or leave an array/object unfinished.");
        return sb.ToString();
    }

    private AiBatchAnalysisResponse? deserializeResponse(
        string responseText,
        IReadOnlyDictionary<string, string> allowedGenreKeys)
    {
        if (tryDeserializeBatchResponse(responseText, out var payload, out var exception))
        {
            return normalizePayload(payload);
        }

        var workingResponse = responseText;
        var currentException = exception!;

        if (tryRepairMalformedNumericJson(workingResponse, out var repairedResponse, out var repairs))
        {
            if (tryDeserializeBatchResponse(repairedResponse, out payload, out var repairedException))
            {
                logger.LogWarning(
                    "Repaired malformed AI JSON at {Path}. Applied {RepairCount} numeric fix(es): {Repairs}. Context: {Context}",
                    currentException.Path ?? "$",
                    repairs.Count,
                    string.Join("; ", repairs.Take(6)),
                    buildJsonErrorContext(responseText, currentException));
                return normalizePayload(payload);
            }

            workingResponse = repairedResponse;
            currentException = repairedException!;
        }

        if (tryNormalizeMalformedGenreJson(workingResponse, currentException, allowedGenreKeys, out var normalizedResponse, out var genreRepairs))
        {
            if (tryDeserializeBatchResponse(normalizedResponse, out payload, out var normalizedException))
            {
                logger.LogWarning(
                    "Normalized malformed AI genre data at {Path}. Applied {RepairCount} genre fix(es): {Repairs}. Context: {Context}",
                    currentException.Path ?? "$",
                    genreRepairs.Count,
                    string.Join("; ", genreRepairs.Take(6)),
                    buildJsonErrorContext(workingResponse, currentException));
                return normalizePayload(payload);
            }

            logger.LogError(
                normalizedException,
                "AI genre normalization did not resolve parse failure. Original path {OriginalPath}, repaired path {RepairedPath}, repairs: {Repairs}, response preview: {Preview}",
                currentException.Path ?? "$",
                normalizedException?.Path ?? "$",
                string.Join("; ", genreRepairs.Take(6)),
                summarizeResponse(normalizedResponse));
            currentException = normalizedException!;
        }

        throw currentException;
    }

    private bool tryDeserializeBatchResponse(
        string responseText,
        out AiBatchAnalysisResponse? payload,
        out JsonException? exception)
    {
        try
        {
            payload = JsonSerializer.Deserialize<AiBatchAnalysisResponse>(responseText, jsonOptions);
            exception = null;
            return true;
        }
        catch (JsonException ex)
        {
            payload = null;
            exception = ex;
            return false;
        }
    }

    private static bool tryRepairMalformedNumericJson(
        string responseText,
        out string repairedResponse,
        out IReadOnlyList<string> repairs)
    {
        var repairNotes = new List<string>();
        repairedResponse = repairableNumericFieldPattern.Replace(responseText, match =>
        {
            var field = match.Groups["field"].Value;
            var originalValue = match.Groups["value"].Value;
            if (!tryNormalizeJsonNumber(originalValue, out var normalizedValue, out var reason))
            {
                return match.Value;
            }

            repairNotes.Add($"{field}: {originalValue} -> {normalizedValue} ({reason})");
            return $"{match.Groups["prefix"].Value}{normalizedValue}";
        });

        repairs = repairNotes;
        return repairNotes.Count > 0 && !string.Equals(responseText, repairedResponse, StringComparison.Ordinal);
    }

    private static bool tryNormalizeJsonNumber(string value, out string normalizedValue, out string reason)
    {
        normalizedValue = value;
        var working = value.Trim();
        var reasons = new List<string>();

        if (working.StartsWith('+'))
        {
            working = working[1..];
            reasons.Add("removed-leading-plus");
        }

        var exponentIndex = working.IndexOfAny(['e', 'E']);
        var exponent = exponentIndex >= 0 ? working[exponentIndex..] : string.Empty;
        var mantissa = exponentIndex >= 0 ? working[..exponentIndex] : working;

        if (mantissa.StartsWith("-.", StringComparison.Ordinal))
        {
            mantissa = $"-0{mantissa[1..]}";
            reasons.Add("added-leading-zero");
        }
        else if (mantissa.StartsWith('.'))
        {
            mantissa = $"0{mantissa}";
            reasons.Add("added-leading-zero");
        }

        if (mantissa.EndsWith('.'))
        {
            mantissa = $"{mantissa}0";
            reasons.Add("completed-trailing-decimal");
        }

        if (mantissa.Contains(','))
        {
            if (mantissa.Contains('.') || mantissa.Count(c => c == ',') != 1)
            {
                reason = string.Empty;
                return false;
            }

            mantissa = mantissa.Replace(',', '.');
            reasons.Add("converted-decimal-comma");
        }

        var sign = string.Empty;
        if (mantissa.StartsWith('-'))
        {
            sign = "-";
            mantissa = mantissa[1..];
        }

        var dotIndex = mantissa.IndexOf('.');
        var integerPart = dotIndex >= 0 ? mantissa[..dotIndex] : mantissa;
        var fractionalPart = dotIndex >= 0 ? mantissa[(dotIndex + 1)..] : string.Empty;

        if (integerPart.Length > 1 && integerPart.StartsWith('0'))
        {
            integerPart = integerPart.TrimStart('0');
            if (integerPart.Length == 0)
            {
                integerPart = "0";
            }

            reasons.Add("trimmed-leading-zeros");
        }

        if (integerPart.Length == 0)
        {
            integerPart = "0";
        }

        normalizedValue = dotIndex >= 0
            ? $"{sign}{integerPart}.{fractionalPart}{exponent}"
            : $"{sign}{integerPart}{exponent}";

        if (!strictJsonNumberPattern.IsMatch(normalizedValue) ||
            string.Equals(normalizedValue, value, StringComparison.Ordinal))
        {
            reason = string.Empty;
            normalizedValue = value;
            return false;
        }

        reason = string.Join(", ", reasons);
        return true;
    }

    private static bool tryNormalizeMalformedGenreJson(
        string responseText,
        JsonException exception,
        IReadOnlyDictionary<string, string> allowedGenreKeys,
        out string normalizedResponse,
        out IReadOnlyList<string> repairs)
    {
        repairs = Array.Empty<string>();
        normalizedResponse = responseText;

        if (!isGenrePath(exception.Path))
        {
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(responseText);
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is not JsonObject rootObject ||
            rootObject["songs"] is not JsonArray songsArray)
        {
            return false;
        }

        var repairNotes = new List<string>();
        for (var songIndex = 0; songIndex < songsArray.Count; songIndex++)
        {
            if (songsArray[songIndex] is not JsonObject songObject ||
                !songObject.TryGetPropertyValue("genres", out var genresNode))
            {
                continue;
            }

            if (genresNode is null)
            {
                songObject["genres"] = new JsonArray();
                repairNotes.Add($"songs[{songIndex}].genres replaced null with []");
                continue;
            }

            if (genresNode is not JsonArray genresArray)
            {
                songObject["genres"] = new JsonArray();
                repairNotes.Add($"songs[{songIndex}].genres replaced non-array value with []");
                continue;
            }

            for (var genreIndex = genresArray.Count - 1; genreIndex >= 0; genreIndex--)
            {
                if (!tryNormalizeGenreEntry(genresArray[genreIndex], allowedGenreKeys, out var action))
                {
                    genresArray.RemoveAt(genreIndex);
                    repairNotes.Add($"songs[{songIndex}].genres[{genreIndex}] removed ({action})");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    repairNotes.Add($"songs[{songIndex}].genres[{genreIndex}] normalized ({action})");
                }
            }
        }

        if (repairNotes.Count == 0)
        {
            return false;
        }

        normalizedResponse = rootObject.ToJsonString();
        repairs = repairNotes;
        return true;
    }

    private static bool tryNormalizeGenreEntry(
        JsonNode? genreNode,
        IReadOnlyDictionary<string, string> allowedGenreKeys,
        out string action)
    {
        if (genreNode is not JsonObject genreObject)
        {
            action = "expected an object";
            return false;
        }

        var changes = new List<string>();

        if (!tryNormalizeGenreKey(genreObject, allowedGenreKeys, changes, out action) ||
            !tryNormalizeGenreScore(genreObject, changes, out action) ||
            !tryNormalizeGenreRank(genreObject, changes, out action))
        {
            return false;
        }

        if (genreObject.TryGetPropertyValue("why", out var whyNode) &&
            whyNode is not null &&
            !tryReadJsonString(whyNode, out _))
        {
            genreObject["why"] = null;
            changes.Add("cleared non-string why");
        }

        action = string.Join(", ", changes);
        return true;
    }

    private static bool tryNormalizeGenreKey(
        JsonObject genreObject,
        IReadOnlyDictionary<string, string> allowedGenreKeys,
        List<string> changes,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!genreObject.TryGetPropertyValue("genreKey", out var genreKeyNode) ||
            !tryReadJsonString(genreKeyNode, out var genreKey) ||
            string.IsNullOrWhiteSpace(genreKey))
        {
            failureReason = "missing genreKey";
            return false;
        }

        if (!allowedGenreKeys.TryGetValue(genreKey, out var canonicalGenreKey))
        {
            failureReason = $"unknown genreKey '{genreKey}'";
            return false;
        }

        if (!string.Equals(genreKey, canonicalGenreKey, StringComparison.Ordinal))
        {
            genreObject["genreKey"] = canonicalGenreKey;
            changes.Add($"canonicalized genreKey '{genreKey}'");
        }

        return true;
    }

    private static bool tryNormalizeGenreScore(JsonObject genreObject, List<string> changes, out string failureReason)
    {
        failureReason = string.Empty;
        if (!genreObject.TryGetPropertyValue("score", out var scoreNode))
        {
            failureReason = "missing score";
            return false;
        }

        if (!tryReadFlexibleDouble(scoreNode, out var score, out var scoreAction))
        {
            failureReason = "invalid score";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(scoreAction))
        {
            genreObject["score"] = score;
            changes.Add(scoreAction);
        }

        return true;
    }

    private static bool tryNormalizeGenreRank(JsonObject genreObject, List<string> changes, out string failureReason)
    {
        failureReason = string.Empty;
        if (!genreObject.TryGetPropertyValue("rank", out var rankNode))
        {
            failureReason = "missing rank";
            return false;
        }

        if (!tryReadFlexibleInt32(rankNode, out var rank, out var rankAction))
        {
            failureReason = "invalid rank";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rankAction))
        {
            genreObject["rank"] = rank;
            changes.Add(rankAction);
        }

        return true;
    }

    private static bool tryReadFlexibleDouble(JsonNode? node, out double value, out string action)
    {
        value = 0;
        action = string.Empty;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out value))
            {
                return double.IsFinite(value);
            }

            if (tryReadJsonString(jsonValue, out var stringValue))
            {
                var trimmed = stringValue.Trim();
                if (strictJsonNumberPattern.IsMatch(trimmed) &&
                    double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    action = "converted score string to number";
                    return true;
                }

                if (tryNormalizeJsonNumber(trimmed, out var normalizedValue, out _) &&
                    double.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    action = "normalized malformed score string";
                    return true;
                }
            }
        }

        return false;
    }

    private static bool tryReadFlexibleInt32(JsonNode? node, out int value, out string action)
    {
        value = 0;
        action = string.Empty;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out value))
            {
                return true;
            }

            if (jsonValue.TryGetValue<long>(out var longValue) &&
                longValue >= int.MinValue &&
                longValue <= int.MaxValue)
            {
                value = (int)longValue;
                action = "normalized rank number";
                return true;
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue) &&
                double.IsFinite(doubleValue) &&
                Math.Abs(doubleValue - Math.Round(doubleValue, MidpointRounding.AwayFromZero)) < 0.000001d &&
                doubleValue >= int.MinValue &&
                doubleValue <= int.MaxValue)
            {
                value = (int)doubleValue;
                action = "normalized fractional rank number";
                return true;
            }

            if (tryReadJsonString(jsonValue, out var stringValue))
            {
                var trimmed = stringValue.Trim();
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    action = "converted rank string to number";
                    return true;
                }

                if (tryNormalizeJsonNumber(trimmed, out var normalizedValue, out _) &&
                    decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue) &&
                    decimal.Truncate(decimalValue) == decimalValue &&
                    decimalValue >= int.MinValue &&
                    decimalValue <= int.MaxValue)
                {
                    value = (int)decimalValue;
                    action = "normalized malformed rank string";
                    return true;
                }
            }
        }

        return false;
    }

    private static bool tryReadJsonString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var rawValue) ||
            rawValue == null)
        {
            return false;
        }

        value = rawValue;
        return true;
    }

    private static bool isGenrePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Contains(".genres", StringComparison.Ordinal);

    private static AiBatchAnalysisResponse? normalizePayload(AiBatchAnalysisResponse? payload)
    {
        if (payload == null)
        {
            return null;
        }

        payload.Songs ??= new List<AiTrackAnalysisResult>();
        foreach (var song in payload.Songs)
        {
            song.Markers ??= new List<AiMarkerResult>();
            song.Genres ??= new List<AiGenreScoreResult>();
        }

        return payload;
    }

    private static string buildJsonErrorContext(string responseText, JsonException exception)
    {
        var lineNumber = exception.LineNumber.GetValueOrDefault();
        var bytePosition = exception.BytePositionInLine.GetValueOrDefault();
        var normalizedText = responseText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalizedText.Split('\n');
        if (lineNumber < 0 || lineNumber >= lines.Length)
        {
            return summarizeResponse(responseText);
        }

        var line = lines[(int)lineNumber];
        var start = Math.Max(0, (int)bytePosition - 40);
        var length = Math.Min(line.Length - start, 80);
        if (length <= 0)
        {
            return summarizeResponse(responseText);
        }

        return line.Substring(start, length).Trim();
    }

    private static string summarizeResponse(string responseText)
    {
        const int maxLength = 240;
        var compact = responseText.ReplaceLineEndings(" ").Trim();
        if (compact.Length <= maxLength)
        {
            return compact;
        }

        return $"{compact[..(maxLength - 1)]}…";
    }

    private static AiBatchAnalysisFailureKind classifyJsonFailure(string responseText, JsonException exception)
    {
        if (exception.Message.Contains("reached end of data", StringComparison.OrdinalIgnoreCase))
        {
            return AiBatchAnalysisFailureKind.TruncatedJson;
        }

        var trimmed = responseText.TrimEnd();
        if (trimmed.Length == 0)
        {
            return AiBatchAnalysisFailureKind.InvalidJson;
        }

        var lastCharacter = trimmed[^1];
        if ((lastCharacter == ',' || lastCharacter == ':' || lastCharacter == '"' || lastCharacter == '{' || lastCharacter == '[') &&
            (exception.Path?.StartsWith("$.songs[", StringComparison.Ordinal) ?? false))
        {
            return AiBatchAnalysisFailureKind.TruncatedJson;
        }

        return AiBatchAnalysisFailureKind.InvalidJson;
    }
}

public enum AiBatchAnalysisFailureKind
{
    InvalidJson,
    TruncatedJson
}

public sealed class AiBatchAnalysisException : Exception
{
    public AiBatchAnalysisException(
        AiBatchAnalysisFailureKind failureKind,
        string jsonPath,
        string responseContext,
        string responsePreview,
        JsonException innerException)
        : base(buildMessage(failureKind, jsonPath, innerException.Message), innerException)
    {
        FailureKind = failureKind;
        JsonPath = jsonPath;
        ResponseContext = responseContext;
        ResponsePreview = responsePreview;
    }

    public AiBatchAnalysisFailureKind FailureKind { get; }
    public string JsonPath { get; }
    public string ResponseContext { get; }
    public string ResponsePreview { get; }
    public bool ShouldRetryIndividually => FailureKind == AiBatchAnalysisFailureKind.TruncatedJson;

    private static string buildMessage(AiBatchAnalysisFailureKind failureKind, string jsonPath, string parserMessage)
    {
        var description = failureKind == AiBatchAnalysisFailureKind.TruncatedJson
            ? "AI returned truncated JSON"
            : "AI returned invalid JSON";
        return $"{description} near {jsonPath}. {parserMessage}";
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
