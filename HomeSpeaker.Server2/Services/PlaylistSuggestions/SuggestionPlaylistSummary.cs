namespace HomeSpeaker.Server2.Services.PlaylistSuggestions;

/// <summary>How far review of one suggested playlist has got.</summary>
public sealed record SuggestionPlaylistSummary(string Name, int Pending, int Approved, int Rejected)
{
    public int Total => Pending + Approved + Rejected;
}
