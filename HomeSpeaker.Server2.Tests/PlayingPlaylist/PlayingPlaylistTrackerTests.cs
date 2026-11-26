using HomeSpeaker.Server2.Services.PlayingPlaylist;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.PlayingPlaylist;

public sealed class PlayingPlaylistTrackerTests
{
    private static Song song(string path) => new() { SongId = 1, Name = Path.GetFileNameWithoutExtension(path), Path = path };

    [Fact]
    public void PlaylistFor_NamesThePlaylist_WhenTheSongCameFromIt()
    {
        var tracker = new PlayingPlaylistTracker();
        tracker.Started("Road Trip", ["/m/a.mp3", "/m/b.mp3"]);

        Assert.Equal("Road Trip", tracker.PlaylistFor(song("/m/b.mp3")));
    }

    [Fact]
    public void PlaylistFor_IsNull_ForASongThatIsNotInThePlaylist()
    {
        var tracker = new PlayingPlaylistTracker();
        tracker.Started("Road Trip", ["/m/a.mp3"]);

        Assert.Null(tracker.PlaylistFor(song("/m/other.mp3")));
        Assert.Null(tracker.PlaylistFor(null));
    }

    [Fact]
    public void PlaylistFor_IsNull_BeforeAnyPlaylistStarts()
    {
        Assert.Null(new PlayingPlaylistTracker().PlaylistFor(song("/m/a.mp3")));
    }

    [Fact]
    public void Forget_EndsThePlaylist()
    {
        var tracker = new PlayingPlaylistTracker();
        tracker.Started("Road Trip", ["/m/a.mp3"]);

        tracker.Forget();

        Assert.Null(tracker.PlaylistFor(song("/m/a.mp3")));
    }

    [Fact]
    public void Removed_DropsOnlyThatSong()
    {
        var tracker = new PlayingPlaylistTracker();
        tracker.Started("Road Trip", ["/m/a.mp3", "/m/b.mp3"]);

        tracker.Removed("/m/a.mp3");

        Assert.Null(tracker.PlaylistFor(song("/m/a.mp3")));
        Assert.Equal("Road Trip", tracker.PlaylistFor(song("/m/b.mp3")));
    }

    [Fact]
    public void ChattyPlayer_ForgetsThePlaylist_WhenTheQueueIsClearedOrAStreamPlays()
    {
        var tracker = new PlayingPlaylistTracker();
        using var player = new ChattyMusicPlayer(new RecordingMusicPlayer(), tracker);

        tracker.Started("Road Trip", ["/m/a.mp3"]);
        player.ClearQueue();
        Assert.Null(tracker.PlaylistFor(song("/m/a.mp3")));

        tracker.Started("Road Trip", ["/m/a.mp3"]);
        player.PlayStream("http://radio.example/stream", "Radio");
        Assert.Null(tracker.PlaylistFor(song("/m/a.mp3")));
    }
}
