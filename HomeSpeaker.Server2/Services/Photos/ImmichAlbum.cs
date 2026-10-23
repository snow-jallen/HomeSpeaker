namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>An album listed by <see cref="ImmichAlbumBrowser"/>.</summary>
public sealed record ImmichAlbum(string Id, string Name, int AssetCount);
