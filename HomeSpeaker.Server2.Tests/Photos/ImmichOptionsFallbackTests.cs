using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichOptionsFallbackTests
{
    [Fact]
    public void Apply_UsesImmichApiKey_WhenSectionKeyMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "IMMICH_API_KEY", "abc" } })
            .Build();
        var options = new ImmichOptions();

        ImmichOptionsFallback.Apply(options, config);

        Assert.Equal("abc", options.ApiKey);
    }

    [Fact]
    public void Apply_KeepsSectionKey_WhenBothSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "IMMICH_API_KEY", "abc" } })
            .Build();
        var options = new ImmichOptions { ApiKey = "section" };

        ImmichOptionsFallback.Apply(options, config);

        Assert.Equal("section", options.ApiKey);
    }

    [Fact]
    public void Apply_LeavesNull_WhenNeitherSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var options = new ImmichOptions();

        ImmichOptionsFallback.Apply(options, config);

        Assert.Null(options.ApiKey);
    }
}
