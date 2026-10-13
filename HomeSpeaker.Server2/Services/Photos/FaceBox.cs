namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>A face bounding box normalized to 0..1 of the image's width/height.</summary>
public sealed record FaceBox(double X1, double Y1, double X2, double Y2);
