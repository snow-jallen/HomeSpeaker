namespace HomeSpeaker.Server2.Services;

/// <summary>A place found by <see cref="LocationSearchService"/>; <see cref="Region"/> is the state or province.</summary>
public sealed record LocationMatch(string Name, string? Region, string? Country, double Latitude, double Longitude);
