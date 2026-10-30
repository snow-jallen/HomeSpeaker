using HomeSpeaker.Server2.Services.AlbumArt;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class AlbumArtSearchServiceTests
{
    private static readonly AlbumArtQuery query = new("Queen", "A Night at the Opera", null);

    [Fact]
    public async Task SearchAsync_OneSourceThrows_ReturnsOthersAndNamesTheFailure()
    {
        var good = new StubSource("Good", [candidate("Good", "https://img/1.jpg", "Queen", "A Night at the Opera")]);
        var bad = new StubSource("Bad", exception: new HttpRequestException("boom"));
        var service = new AlbumArtSearchService([bad, good], NullLogger<AlbumArtSearchService>.Instance);

        var result = await service.SearchAsync(query, CancellationToken.None);

        var only = Assert.Single(result.Candidates);
        Assert.Equal("Good", only.Source);
        Assert.Equal(["Bad"], result.FailedSources);
    }

    [Fact]
    public async Task SearchAsync_ScoresAndSortsBestFirst_DroppingDuplicateUrls()
    {
        var first = new StubSource("A",
        [
            candidate("A", "https://img/wrong.jpg", "Queen", "Jazz"),
            candidate("A", "https://img/remaster.jpg", "Queen", "A Night At The Opera (2011 Remaster)"),
            candidate("A", "https://img/right.jpg", "Queen", "A Night at the Opera"),
        ]);
        var second = new StubSource("B", [candidate("B", "https://img/right.jpg", "Queen", "A Night at the Opera")]);
        var service = new AlbumArtSearchService([first, second], NullLogger<AlbumArtSearchService>.Instance);

        var result = await service.SearchAsync(query, CancellationToken.None);

        Assert.Equal(["https://img/right.jpg", "https://img/remaster.jpg", "https://img/wrong.jpg"], result.Candidates.Select(c => c.ImageUrl));
        Assert.Equal(1.0, result.Candidates[0].Score);
        Assert.True(result.Candidates[2].Score < 0.8, $"wrong album scored {result.Candidates[2].Score}");
        Assert.Empty(result.FailedSources);
    }

    [Fact]
    public async Task SearchAsync_CallerCancels_Throws()
    {
        var service = new AlbumArtSearchService([new StubSource("Slow", delay: TimeSpan.FromSeconds(30))], NullLogger<AlbumArtSearchService>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(query, cts.Token));
    }

    private static AlbumArtCandidate candidate(string source, string url, string artist, string album) =>
        new(source, url, url, artist, album, null);

    private sealed class StubSource(string name, IReadOnlyList<AlbumArtCandidate>? candidates = null, Exception? exception = null, TimeSpan delay = default) : IAlbumArtSource
    {
        public string Name => name;

        public async Task<IReadOnlyList<AlbumArtCandidate>> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return exception is null ? candidates ?? [] : throw exception;
        }
    }
}
