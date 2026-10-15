namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>The second photo of a side-by-side portrait pair.</summary>
public sealed record PairedPhoto(string Id, string Url, IReadOnlyList<FaceBox> Faces);
