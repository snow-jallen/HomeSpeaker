using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Tests;

public sealed class SafeFileNameTests
{
    [Fact]
    public void OrdinaryTitleIsKept()
    {
        Assert.Equal("Song Title (Live).mp3", SafeFileName.From("Song Title (Live).mp3"));
    }

    [Theory]
    [InlineData("x\" noiseprof /music/HomeSpeaker.db \".mp3")]
    [InlineData("a/b\\c:d*e?f<g>h|i.mp3")]
    [InlineData("tab\there\nnewline.mp3")]
    public void CharactersUnsafeOnAnyPlatformAreReplaced(string title)
    {
        var name = SafeFileName.From(title);

        Assert.DoesNotContain(name, c => c is '"' or '/' or '\\' or ':' or '*' or '?' or '<' or '>' or '|' || char.IsControl(c));
    }

    [Fact]
    public void TitleOfOnlyDotsDoesNotBecomeARelativePath()
    {
        Assert.NotEqual("..", SafeFileName.From(".."));
    }
}
