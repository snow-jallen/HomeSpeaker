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
}
