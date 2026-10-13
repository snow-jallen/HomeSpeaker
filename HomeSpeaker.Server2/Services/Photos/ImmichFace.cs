namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>A face bounding box in pixels of the image Immich measured it on.</summary>
public sealed record ImmichFace(double X1, double Y1, double X2, double Y2, int ImageWidth, int ImageHeight);
