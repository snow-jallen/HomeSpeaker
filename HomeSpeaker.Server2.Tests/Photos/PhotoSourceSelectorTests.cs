using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoSourceSelectorTests
{
    private static readonly ImmichOptions configured = new() { BaseUrl = "https://immich.test", ApiKey = "key", AlbumId = "album" };

    private static IConfiguration config(string? source = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(source is null ? [] : new Dictionary<string, string?> { ["Photos:Source"] = source })
            .Build();

    [Fact]
    public void Resolve_ImmichConfigured_Immich() =>
        Assert.Equal(PhotoSourceKind.Immich, PhotoSourceSelector.Resolve(config(), configured));

    [Fact]
    public void Resolve_NothingConfigured_Folder() =>
        Assert.Equal(PhotoSourceKind.Folder, PhotoSourceSelector.Resolve(config(), new ImmichOptions()));

    [Fact]
    public void Resolve_ExplicitFolder_WinsOverImmich() =>
        Assert.Equal(PhotoSourceKind.Folder, PhotoSourceSelector.Resolve(config("FOLDER"), configured));

    [Fact]
    public void Resolve_ExplicitImmich_IsCaseInsensitive() =>
        Assert.Equal(PhotoSourceKind.Immich, PhotoSourceSelector.Resolve(config("immich"), new ImmichOptions()));

    [Fact]
    public void Resolve_UnknownValue_FallsBackToDefaultRule() =>
        Assert.Equal(PhotoSourceKind.Immich, PhotoSourceSelector.Resolve(config("dropbox"), configured));
}
