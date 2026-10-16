using HomeSpeaker.Server2.Services.MediaSearch;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.MediaSearch;

public sealed class MediaMatcherTests
{
    private static readonly MediaCandidate[] library =
    [
        new(MediaKind.Playlist, "Hymns for Home and Church", "Hymns for Home and Church"),
        new(MediaKind.Playlist, "King's Singers", "King's Singers"),
        new(MediaKind.Stream, "Classical Kids", "3"),
        new(MediaKind.Stream, "KBAQ", "4"),
        new(MediaKind.Mix, "Hymns", "hymns"),
        new(MediaKind.Mix, "Quiet Classical", "quiet-classical"),
        new(MediaKind.Artist, "The King's Singers", "The King's Singers"),
        new(MediaKind.Album, "Gold", "Gold", "The King's Singers"),
        new(MediaKind.Song, "1039 - Because", "4069", "Hymns for Home and Church"),
        new(MediaKind.Song, "Because", "17", "The Beatles"),
        new(MediaKind.Song, "Gold", "18", "Spandau Ballet"),
    ];

    private static MediaCandidate? match(string text, MediaKind? kind = null, string? artist = null) =>
        MediaMatcher.Match(library, new MediaQuery(text, kind, artist));

    [Theory]
    [InlineData("Kings Singers", MediaKind.Playlist)]
    [InlineData("classical kids", MediaKind.Stream)]
    [InlineData("quiet classical", MediaKind.Mix)]
    [InlineData("Hymns", MediaKind.Mix)]
    public void ExactNamesWin(string text, MediaKind expected) => Assert.Equal(expected, match(text)!.Kind);

    [Fact]
    public void EqualScoresPreferPlaylistsOverArtists()
    {
        // "the" is dropped, so the playlist and the artist both match exactly.
        Assert.Equal(MediaKind.Playlist, match("the kings singers")!.Kind);
    }

    [Fact]
    public void KindHintFromSiriPicksTheArtist() =>
        Assert.Equal(MediaKind.Artist, match("kings singers", MediaKind.Artist)!.Kind);

    [Theory]
    [InlineData("hymns playlist", "Hymns for Home and Church")]
    [InlineData("the album gold", "Gold")]
    [InlineData("KBAQ radio", "KBAQ")]
    public void KindWordsInTheRequestAreHints(string text, string expectedName)
    {
        var result = match(text)!;
        Assert.Equal(expectedName, result.Name);
        Assert.NotEqual(MediaKind.Song, result.Kind);
    }

    [Fact]
    public void NamesEndingInAKindWordStillMatchExactly()
    {
        MediaCandidate[] christmas =
        [
            new(MediaKind.Mix, "Christmas", "christmas"),
            new(MediaKind.Playlist, "Christmas Mix", "Christmas Mix"),
        ];

        Assert.Equal(MediaKind.Playlist, MediaMatcher.Match(christmas, new MediaQuery("christmas mix"))!.Kind);
    }

    [Fact]
    public void ArtistNarrowsSongs() =>
        Assert.Equal("17", match("because", artist: "Beatles")!.Key);

    [Fact]
    public void ArtistAloneMeansTheArtist() =>
        Assert.Equal(MediaKind.Artist, match("", artist: "King's Singers")!.Kind);

    [Fact]
    public void TrackNumbersAreIgnored() =>
        Assert.Equal("4069", match("because", artist: "hymns for home")!.Key);

    [Fact]
    public void SmallMistranscriptionsStillMatch() =>
        Assert.Equal("3", match("classical kid")!.Key);

    [Fact]
    public void PartialNamesMatchByLeadingWords() =>
        Assert.Equal("Hymns for Home and Church", match("hymns for home")!.Name);

    [Fact]
    public void UnrelatedRequestsMatchNothing() => Assert.Null(match("polka party"));

    [Fact]
    public void EmptyRequestsMatchNothing() => Assert.Null(match("  "));

    [Theory]
    [InlineData("1039 - King's Singers & Friends", "kings singers and friends")]
    [InlineData("The Beatles", "beatles")]
    [InlineData("AC/DC", "ac dc")]
    public void NormalizeKeepsOnlyWords(string input, string expected) =>
        Assert.Equal(expected, MediaMatcher.Normalize(input));
}
