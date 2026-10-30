using HomeSpeaker.Server2.Services.AlbumArt;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class MissingAlbumArtFinderTests
{
    [Fact]
    public async Task GetGroupsAsync_GroupsArtlessSongsByArtistAndAlbum()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var track1 = fixture.Add("queen/opera/01.mp3", "Death on Two Legs", "Queen", "A Night at the Opera");
        var track2 = fixture.Add("queen/opera/02.mp3", "Lazing on a Sunday Afternoon", "queen", "a night at the opera");
        fixture.Add("queen/opera/03.mp3", "I'm in Love with My Car", "Queen", "A Night at the Opera", withArt: true);
        var jazz = fixture.Add("queen/jazz/01.mp3", "Mustapha", "Queen", "Jazz");
        fixture.Add("abba/arrival/01.mp3", "Dancing Queen", "ABBA", "Arrival", withArt: true);
        var single = fixture.Add("loose/song.mp3", "Loose Song", "Somebody", null);
        var unknown = fixture.Add("loose/unknown.mp3", "Mystery", null, null);

        var groups = await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);

        Assert.Equal(4, groups.Count);
        var opera = Assert.Single(groups, g => g.Album == "A Night at the Opera" || g.Album == "a night at the opera");
        Assert.True(opera.IsAlbum);
        Assert.Equal([track1, track2], opera.Songs.Select(s => s.Path));
        Assert.Equal(new AlbumArtQuery(opera.Artist, opera.Album, null), opera.Query);

        Assert.Equal([jazz], Assert.Single(groups, g => g.Album == "Jazz").Songs.Select(s => s.Path));

        var loose = Assert.Single(groups, g => g.Artist == "Somebody");
        Assert.False(loose.IsAlbum);
        Assert.Equal([single], loose.Songs.Select(s => s.Path));
        Assert.Equal(new AlbumArtQuery("Somebody", null, "Loose Song"), loose.Query);

        var mystery = Assert.Single(groups, g => g.Artist == AlbumArtGroup.UnknownArtist);
        Assert.Equal([unknown], mystery.Songs.Select(s => s.Path));
        Assert.Equal(new AlbumArtQuery(null, null, "Mystery"), mystery.Query);
    }

    [Fact]
    public async Task GetGroupsAsync_UnchangedLibrary_ReturnsCachedList()
    {
        using var fixture = new AlbumArtLibraryFixture();
        fixture.Add("a.mp3", "A", "Artist", "Album");

        var first = await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);
        var second = await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetGroupsAsync_AfterLibraryRescan_SeesNewSongs()
    {
        using var fixture = new AlbumArtLibraryFixture();
        fixture.Add("a.mp3", "A", "Artist", "Album");
        Assert.Single(await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None));

        fixture.Add("b.mp3", "B", "Other", "Other Album");
        fixture.Library.MarkDirty("test");
        var groups = await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public async Task GetGroupsAsync_Rescan_PicksUpArtAddedOutsideTheApp()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var path = fixture.Add("a.mp3", "A", "Artist", "Album");
        Assert.Single(await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None));

        TestMp3.Create(fixture.Folder, "a.mp3", "A", "Artist", "Album", TestMp3.Jpeg);
        var groups = await fixture.Finder.GetGroupsAsync(rescan: true, CancellationToken.None);

        Assert.Empty(groups);
        Assert.Single(TestMp3.Pictures(path));
    }

    [Fact]
    public async Task ForgetSongs_RemovesSongsAndEmptyGroupsFromTheCache()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var a1 = fixture.Add("a1.mp3", "A1", "Artist", "Album");
        var a2 = fixture.Add("a2.mp3", "A2", "Artist", "Album");
        var b = fixture.Add("b.mp3", "B", "Artist", "Other");
        await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);

        fixture.Finder.ForgetSongs([a1, b]);
        var groups = await fixture.Finder.GetGroupsAsync(rescan: false, CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal([a2], group.Songs.Select(s => s.Path));
    }

    [Fact]
    public void Group_KeysIgnoreCaseAndSongsWithoutAlbumStandAlone()
    {
        var groups = MissingAlbumArtFinder.Group(
        [
            new Song { Name = "One", Path = "/m/1.mp3", Artist = "Queen", Album = "Jazz" },
            new Song { Name = "Two", Path = "/m/2.mp3", Artist = " QUEEN ", Album = "jazz " },
            new Song { Name = "Three", Path = "/m/3.mp3", Artist = "Queen", Album = "" },
            new Song { Name = "Four", Path = "/m/4.mp3", Artist = "Queen", Album = null },
            new Song { Name = "No path", Path = null, Artist = "Queen", Album = "Jazz" },
        ]);

        Assert.Equal(3, groups.Count);
        var jazz = Assert.Single(groups, g => g.IsAlbum);
        Assert.Equal(2, jazz.Songs.Count);
        Assert.Equal(AlbumArtGroup.KeyFor("Queen", "Jazz", "/m/1.mp3"), jazz.Key);
        Assert.Equal(["song:/m/3.mp3", "song:/m/4.mp3"], groups.Where(g => !g.IsAlbum).Select(g => g.Key).Order(StringComparer.Ordinal));
    }
}
