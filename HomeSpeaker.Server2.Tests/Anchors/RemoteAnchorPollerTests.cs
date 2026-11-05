using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class RemoteAnchorPollerTests
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private static AnchorBoardDto board(string state) => new(today, today, today,
    [
        new AnchorPersonDto("1", "Sam", "teal", null, "local", 0, 0, 1,
            [new AnchorDto("1", "Walk", "day", 0, null, DayMask.Every, false, [], "local", true, true, 0, [new AnchorCellDto(today, state)])]),
    ]);

    [Fact]
    public void Changed_FirstBoard_IsNotAChange()
    {
        var watcher = new AnchorBoardWatcher();

        Assert.False(watcher.Changed(board("open")));
    }

    [Fact]
    public void Changed_SameBoard_False_DifferentBoard_True()
    {
        var watcher = new AnchorBoardWatcher();
        watcher.Changed(board("open"));

        Assert.False(watcher.Changed(board("open")));
        Assert.True(watcher.Changed(board("kept")));
        Assert.False(watcher.Changed(board("kept")));
    }

    [Fact]
    public void Changed_StaleFlagAloneIsNotAChange()
    {
        var watcher = new AnchorBoardWatcher();
        watcher.Changed(board("open"));

        Assert.False(watcher.Changed(board("open") with { Stale = true }));
    }

    [Fact]
    public void Notifier_KnowsWhenSomeoneListens()
    {
        var notifier = new AnchorChangeNotifier();
        Assert.False(notifier.HasListeners);

        void handler() { }
        notifier.Changed += handler;
        Assert.True(notifier.HasListeners);
        notifier.Changed -= handler;
        Assert.False(notifier.HasListeners);
    }
}
