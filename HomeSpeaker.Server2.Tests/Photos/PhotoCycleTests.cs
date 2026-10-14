using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCycleTests
{
    [Fact]
    public void Next_WhenEmpty_ReturnsNull() => Assert.Null(new PhotoCycle(new Random(1)).Next());

    [Fact]
    public void Next_ShowsEveryPhotoOnceBeforeRepeating()
    {
        var cycle = new PhotoCycle(new Random(42));
        string[] ids = ["a", "b", "c", "d", "e"];
        cycle.SetAvailable(ids);

        var round = Enumerable.Range(0, 5).Select(_ => cycle.Next()!).ToList();

        Assert.Equal(ids.Order(), round.Order());
    }

    [Fact]
    public void Next_NeverRepeatsBackToBackAcrossRounds()
    {
        var cycle = new PhotoCycle(new Random(7));
        cycle.SetAvailable(["a", "b", "c"]);
        string? previous = null;

        for (var i = 0; i < 300; i++)
        {
            var current = cycle.Next();
            Assert.NotEqual(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void SetAvailable_MidRound_KeepsShownOutAndAddsNew()
    {
        var cycle = new PhotoCycle(new Random(3));
        cycle.SetAvailable(["a", "b", "c", "d"]);
        var shown = new[] { cycle.Next()!, cycle.Next()! };
        var unshown = new[] { "a", "b", "c", "d" }.Except(shown).ToList();

        // Remove one unshown photo, add a brand-new one.
        cycle.SetAvailable(shown.Concat([unshown[0], "z"]));
        var rest = new[] { cycle.Next()!, cycle.Next()! };

        Assert.Equal(new[] { unshown[0], "z" }.Order(), rest.Order());
    }

    [Fact]
    public void Next_SinglePhoto_RepeatsIt()
    {
        var cycle = new PhotoCycle(new Random(1));
        cycle.SetAvailable(["only"]);
        Assert.Equal("only", cycle.Next());
        Assert.Equal("only", cycle.Next());
    }

    [Fact]
    public void Peek_WhenEmpty_ReturnsNothing() => Assert.Empty(new PhotoCycle(new Random(1)).Peek(5));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    public void Peek_MatchesWhatNextReturns_IncludingAcrossRoundBoundaries(int peekCount)
    {
        var cycle = new PhotoCycle(new Random(11));
        cycle.SetAvailable(["a", "b", "c", "d", "e", "f"]);

        var peeks = new List<IReadOnlyList<string>>();
        var actual = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            peeks.Add(cycle.Peek(peekCount));
            actual.Add(cycle.Next()!);
        }

        for (var i = 0; i < peeks.Count; i++)
        {
            Assert.NotEmpty(peeks[i]);
            Assert.Equal(actual.Skip(i).Take(peeks[i].Count), peeks[i].Take(actual.Count - i));
        }
    }

    [Fact]
    public void Peek_DoesNotConsumeAnything()
    {
        var cycle = new PhotoCycle(new Random(5));
        cycle.SetAvailable(["a", "b", "c", "d"]);

        var first = cycle.Peek(4);
        var second = cycle.Peek(4);
        var next = Enumerable.Range(0, 4).Select(_ => cycle.Next()!).ToList();

        Assert.Equal(first, second);
        Assert.Equal(first, next);
    }

    [Fact]
    public void Peek_PastTheEndOfARound_ShowsTheNextRoundsOrderAndNeverStartsItWithTheLastPhoto()
    {
        var cycle = new PhotoCycle(new Random(2));
        cycle.SetAvailable(["a", "b", "c"]);
        var ordered = new List<string> { cycle.Next()!, cycle.Next()! };

        var peeked = cycle.Peek(5);

        Assert.Equal(4, peeked.Count);
        Assert.NotEqual(ordered[^1], peeked[0]);
        Assert.NotEqual(peeked[0], peeked[1]);
        Assert.NotEqual(peeked[1], peeked[2]);
        Assert.NotEqual(peeked[0], peeked[2]);
        Assert.NotEqual(peeked[2], peeked[3]);
        Assert.Equal(peeked, Enumerable.Range(0, 4).Select(_ => cycle.Next()!).ToList());
    }

    [Fact]
    public void Peek_AfterSetAvailable_ReflectsTheNewSet()
    {
        var cycle = new PhotoCycle(new Random(9));
        cycle.SetAvailable(["a", "b", "c"]);
        cycle.Peek(5);

        cycle.SetAvailable(["x", "y"]);
        var peeked = cycle.Peek(2);

        Assert.Equal(["x", "y"], peeked.Order());
        Assert.Equal(peeked, [cycle.Next()!, cycle.Next()!]);
    }
}
