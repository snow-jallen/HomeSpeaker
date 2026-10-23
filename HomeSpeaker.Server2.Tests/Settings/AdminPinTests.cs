using HomeSpeaker.Server2.Settings;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Settings;

public class AdminPinTests
{
    [Fact]
    public void Hash_ThenVerify_RoundTrips()
    {
        var hash = AdminPin.Hash("1234");

        Assert.StartsWith("pbkdf2-sha256$100000$", hash, StringComparison.Ordinal);
        Assert.True(AdminPin.Verify("1234", hash));
    }

    [Fact]
    public void Verify_WrongPin_False() => Assert.False(AdminPin.Verify("4321", AdminPin.Hash("1234")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("pbkdf2-sha256$100000$!!!$???")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("md5$100000$AAAA$AAAA")]
    public void Verify_MalformedHash_False(string? stored) => Assert.False(AdminPin.Verify("1234", stored));

    [Fact]
    public void Hash_SamePinTwice_DifferentSalts() => Assert.NotEqual(AdminPin.Hash("1234"), AdminPin.Hash("1234"));

    [Theory]
    [InlineData("1234", true)]
    [InlineData("123", false)]
    [InlineData("12a4", false)]
    [InlineData("1234567890123", false)]
    [InlineData("123456789012", true)]
    [InlineData(null, false)]
    public void IsValidFormat_Checks4To12Digits(string? pin, bool expected) => Assert.Equal(expected, AdminPin.IsValidFormat(pin));

    [Fact]
    public void IsSet_EmptyHash_False()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:PinHash"] = "" }).Build();

        Assert.False(AdminPin.IsSet(config));
    }

    [Fact]
    public void IsSet_WithHash_True()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:PinHash"] = AdminPin.Hash("1234") }).Build();

        Assert.True(AdminPin.IsSet(config));
    }

    [Theory]
    [InlineData("/settings", "/settings")]
    [InlineData("//evil.com", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("/\\evil", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    public void SafeReturnUrl_OnlyAllowsLocalPaths(string? input, string expected) => Assert.Equal(expected, AdminPin.SafeReturnUrl(input));
}
