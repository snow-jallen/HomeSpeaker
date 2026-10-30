namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Asks every <see cref="IAlbumArtSource"/> at once and scores what they return. One failing or slow source never sinks the others.</summary>
public sealed class AlbumArtSearchService
{
    /// <summary>Per source, including any wait for its throttle.</summary>
    public static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(20);

    private readonly IReadOnlyList<IAlbumArtSource> sources;
    private readonly ILogger<AlbumArtSearchService> logger;

    public AlbumArtSearchService(IEnumerable<IAlbumArtSource> sources, ILogger<AlbumArtSearchService> logger)
    {
        this.sources = sources.ToList();
        this.logger = logger;
    }

    public async Task<AlbumArtSearchResult> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(sources.Select(source => searchOneAsync(source, query, cancellationToken))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = results
            .SelectMany(result => result.Candidates)
            .Where(candidate => seen.Add(candidate.ImageUrl))
            .Select(candidate => candidate with { Score = AlbumArtScore.Score(query, candidate.Artist, candidate.Album, candidate.Title) })
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
        var failed = results.Where(result => result.Failed).Select(result => result.Source).ToList();
        logger.LogInformation(
            "Album art search for {Artist} / {Name}: {Count} candidates, failed sources: {Failed}",
            query.Artist, query.Name, candidates.Count, failed.Count == 0 ? "none" : string.Join(", ", failed));
        return new AlbumArtSearchResult(candidates, failed);
    }

    private async Task<(string Source, IReadOnlyList<AlbumArtCandidate> Candidates, bool Failed)> searchOneAsync(
        IAlbumArtSource source, AlbumArtQuery query, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SourceTimeout);
        try
        {
            var candidates = await source.SearchAsync(query, timeout.Token).ConfigureAwait(false);
            return (source.Name, candidates, false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Album art source {Source} failed for {Artist} / {Name}", source.Name, query.Artist, query.Name);
            return (source.Name, [], true);
        }
    }
}
