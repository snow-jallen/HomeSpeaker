using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// What the overlay and Home page show for the current track or stream. For a stream, Station is the
/// station name (the art is looked up and generated from it); it is null for songs.
/// </summary>
public sealed record NowPlayingInfo(string Title, string? Subtitle, int? SongId, bool IsStream, double Percent, TimeSpan Elapsed, TimeSpan Remaining, string? Station = null)
{
    public static NowPlayingInfo? From(PlayerStatus? status)
    {
        if (status is not { StillPlaying: true })
        {
            return null;
        }

        var songName = status.CurrentSong?.Name ?? string.Empty;
        if (status.IsStream)
        {
            var station = string.IsNullOrWhiteSpace(status.StreamName) ? "Radio" : status.StreamName;
            var hasIcyTitle = songName.Length > 0 && !string.Equals(songName, station, StringComparison.Ordinal);
            return new NowPlayingInfo(hasIcyTitle ? songName : station, hasIcyTitle ? station : "Live radio", null, true, 0, status.Elapsed, TimeSpan.Zero, station);
        }

        if (status.CurrentSong is null)
        {
            return null;
        }

        var parts = new[] { clean(status.CurrentSong.Artist, "[ No Artist ]", "[Artist Unknown]"), clean(status.CurrentSong.Album, "[ No Album ]") }
            .Where(p => p is not null);
        var subtitle = string.Join(" · ", parts);
        return new NowPlayingInfo(
            songName,
            subtitle.Length > 0 ? subtitle : null,
            status.CurrentSong.SongId,
            false,
            (double)status.PercentComplete,
            status.Elapsed,
            status.Remaining);
    }

    private static string? clean(string? value, params string[] placeholders) =>
        string.IsNullOrWhiteSpace(value) || placeholders.Contains(value, StringComparer.Ordinal) ? null : value;
}
