namespace HomeSpeaker.Server2.Data;

/// <summary>
/// A song proposed for a playlist, waiting for someone to approve or reject it on the
/// playlist suggestions page. Approving adds the song to the real playlist named
/// <see cref="PlaylistName"/>; a rejected row stays, so the same song isn't suggested again.
/// </summary>
public class PlaylistSuggestion
{
    public int Id { get; set; }
    public string PlaylistName { get; set; } = string.Empty;
    public string SongPath { get; set; } = string.Empty;
    public SuggestionStatus Status { get; set; }

    /// <summary>Why the song was suggested, shown to the reviewer.</summary>
    public string? Reason { get; set; }

    /// <summary>Review order within the playlist; the most likely fits come first.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? ReviewedUtc { get; set; }
}
