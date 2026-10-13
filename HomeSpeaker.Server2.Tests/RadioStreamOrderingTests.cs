namespace HomeSpeaker.Server2.Tests;

public sealed class RadioStreamOrderingTests
{
    private sealed class TestStream
    {
        public required int PlayCount { get; init; }
        public required string Name { get; init; }
    }

    [Fact]
    public void HigherPlayCountComesFirst()
    {
        var streams = new[]
        {
            new TestStream { PlayCount = 1, Name = "A" },
            new TestStream { PlayCount = 5, Name = "B" },
            new TestStream { PlayCount = 3, Name = "C" }
        };

        var result = Services.RadioStreamOrdering.ByPopularity(
            streams,
            s => s.PlayCount,
            s => s.Name
        ).ToList();

        Assert.Equal("B", result[0].Name);
        Assert.Equal("C", result[1].Name);
        Assert.Equal("A", result[2].Name);
    }

    [Fact]
    public void TiesBrokenByNameCaseInsensitively()
    {
        var streams = new[]
        {
            new TestStream { PlayCount = 5, Name = "charlie" },
            new TestStream { PlayCount = 5, Name = "Alice" },
            new TestStream { PlayCount = 5, Name = "bob" }
        };

        var result = Services.RadioStreamOrdering.ByPopularity(
            streams,
            s => s.PlayCount,
            s => s.Name
        ).ToList();

        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("bob", result[1].Name);
        Assert.Equal("charlie", result[2].Name);
    }

    [Fact]
    public void EmptyInputGivesEmptyOutput()
    {
        var streams = Array.Empty<TestStream>();

        var result = Services.RadioStreamOrdering.ByPopularity(
            streams,
            s => s.PlayCount,
            s => s.Name
        ).ToList();

        Assert.Empty(result);
    }
}
