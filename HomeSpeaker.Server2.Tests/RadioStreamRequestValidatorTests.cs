using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Tests;

public sealed class RadioStreamRequestValidatorTests
{
    [Fact]
    public void NameAndUrlTogetherAreAccepted()
    {
        Assert.Null(RadioStreamRequestValidator.Validate("KBAQ", "http://example.com/kbaq"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameIsRejected(string? name)
    {
        Assert.NotNull(RadioStreamRequestValidator.Validate(name, "http://example.com/kbaq"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankUrlIsRejected(string? url)
    {
        Assert.NotNull(RadioStreamRequestValidator.Validate("KBAQ", url));
    }

    [Theory]
    [InlineData("file:///certs/certificate.pfx")]
    [InlineData("not a url")]
    public void NonHttpUrlIsRejected(string url)
    {
        Assert.NotNull(RadioStreamRequestValidator.Validate("KBAQ", url));
    }
}
