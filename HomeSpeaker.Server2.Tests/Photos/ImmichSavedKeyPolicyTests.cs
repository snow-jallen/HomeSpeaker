using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichSavedKeyPolicyTests
{
    [Theory]
    [InlineData("https://immich.example.com", "https://immich.example.com")]
    [InlineData(" https://immich.example.com/ ", "https://immich.example.com")]
    [InlineData("HTTPS://Immich.Example.com", "https://immich.example.com/")]
    [InlineData("https://example.com/immich/", "https://example.com/immich")]
    public void Allows_SameAddress_UsesSavedKey(string typed, string saved) =>
        Assert.True(ImmichSavedKeyPolicy.Allows(typed, saved));

    [Theory]
    [InlineData("https://evil.example.com", "https://immich.example.com")]
    [InlineData("http://immich.example.com", "https://immich.example.com")]
    [InlineData("https://immich.example.com:8443", "https://immich.example.com")]
    [InlineData("https://immich.example.com.evil.com", "https://immich.example.com")]
    [InlineData("https://immich.example.com/other", "https://immich.example.com")]
    [InlineData("https://immich.example.com", null)]
    [InlineData("https://immich.example.com", "")]
    [InlineData("", "")]
    [InlineData("not a url", "not a url")]
    public void Allows_DifferentOrMissingAddress_RequiresTypedKey(string? typed, string? saved) =>
        Assert.False(ImmichSavedKeyPolicy.Allows(typed, saved));
}
