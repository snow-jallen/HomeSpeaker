namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// A picture an online source offers for a group. <see cref="ImageUrl"/> is the full-size image that Apply downloads;
/// <see cref="ThumbnailUrl"/> is what the review page shows. <see cref="Score"/> (0 to 1) is how closely the source's
/// artist and album (or title) match the group's.
/// </summary>
public sealed record AlbumArtCandidate(
    string Source,
    string ImageUrl,
    string ThumbnailUrl,
    string? Artist,
    string? Album,
    string? Title,
    double Score = 0);
