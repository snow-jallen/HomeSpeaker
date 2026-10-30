using HomeSpeaker.Server2.Services.AlbumArt;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class AlbumArtScoreTests
{
    [Fact]
    public void Score_SameAlbumIgnoringCase_IsOne()
    {
        Assert.Equal(1.0, AlbumArtScore.Score(new AlbumArtQuery("Queen", "A Night at the Opera", null), "queen", "A NIGHT AT THE OPERA", null));
    }

    [Theory]
    [InlineData("A Night At The Opera (2011 Remaster)")]
    [InlineData("A Night at the Opera [Deluxe Edition]")]
    public void Score_SameAlbumWithEditionNotes_IsHighButBelowExact(string album)
    {
        var score = AlbumArtScore.Score(new AlbumArtQuery("Queen", "A Night at the Opera", null), "Queen", album, null);

        Assert.InRange(score, 0.9, 0.99);
    }

    [Fact]
    public void Score_DifferentArtistAndAlbum_IsLow()
    {
        var score = AlbumArtScore.Score(new AlbumArtQuery("Queen", "A Night at the Opera", null), "ABBA", "Arrival", null);

        Assert.True(score < 0.3, $"scored {score}");
    }

    [Fact]
    public void Score_RightAlbumWrongArtist_IsAboutHalf()
    {
        var score = AlbumArtScore.Score(new AlbumArtQuery("Queen", "Greatest Hits", null), "ABBA", "Greatest Hits", null);

        Assert.InRange(score, 0.4, 0.6);
    }

    [Fact]
    public void Score_UnknownArtist_ComparesOnlyTheTitle()
    {
        Assert.Equal(1.0, AlbumArtScore.Score(new AlbumArtQuery(null, null, "Bohemian Rhapsody"), "Queen", "Whatever", "Bohemian Rhapsody"));
    }

    [Fact]
    public void Score_FreeText_ComparesWithArtistAndAlbumTogether()
    {
        var query = AlbumArtQuery.FreeText("Queen A Night at the Opera");

        Assert.Equal(1.0, AlbumArtScore.Score(query, "Queen", "A Night at the Opera", null));
        Assert.True(AlbumArtScore.Score(query, "ABBA", "Arrival", null) < 0.3);
    }

    [Fact]
    public void FreeText_IsAnAlbumLookupNamedByTheTrimmedWords()
    {
        var query = AlbumArtQuery.FreeText("  night opera ");

        Assert.True(query.IsFreeText);
        Assert.True(query.IsAlbum);
        Assert.Equal("night opera", query.Name);
        Assert.Null(query.Artist);
        Assert.False(new AlbumArtQuery("Queen", "A Night at the Opera", null).IsFreeText);
    }

    [Fact]
    public void Clean_StripsAccentsAndEditionNotes()
    {
        Assert.Equal("bjork debut", AlbumArtScore.Clean("Björk - Début (Remastered)"));
    }
}
