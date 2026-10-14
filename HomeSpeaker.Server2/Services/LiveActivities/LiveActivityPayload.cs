using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>Builds the APNs payloads for Live Activity update and end pushes.</summary>
public static class LiveActivityPayload
{
    /// <summary>How long iOS should treat an update as current before showing it as stale.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    public static LiveActivityContentState CreateContentState(PlayerStatusSnapshot snapshot)
    {
        var status = snapshot.Status;
        var title = !string.IsNullOrWhiteSpace(status.CurrentSong?.Name) ? status.CurrentSong.Name
            : !string.IsNullOrWhiteSpace(status.StreamName) ? status.StreamName
            : status.StillPlaying ? "Streaming"
            : "Not Playing";

        return new LiveActivityContentState(
            Title: title,
            Artist: status.CurrentSong?.Artist ?? string.Empty,
            IsPlaying: status.StillPlaying,
            IsStream: status.IsStream,
            Volume: status.Volume,
            RepeatMode: status.RepeatMode,
            SleepTimerEndsAtUnix: snapshot.SleepTimerEndsAt is { } endsAt ? toUnixSeconds(endsAt) : null,
            UpdatedAtUnix: toUnixSeconds(snapshot.CapturedAt));
    }

    public static string BuildUpdate(LiveActivityContentState contentState, DateTimeOffset now)
    {
        var aps = new JsonObject
        {
            ["timestamp"] = now.ToUnixTimeSeconds(),
            ["event"] = "update",
            ["stale-date"] = (now + StaleAfter).ToUnixTimeSeconds(),
            ["content-state"] = JsonSerializer.SerializeToNode(contentState, serializerOptions)
        };

        return new JsonObject { ["aps"] = aps }.ToJsonString(serializerOptions);
    }

    /// <summary>An end push that also dismisses the activity from the lock screen right away.</summary>
    public static string BuildEnd(LiveActivityContentState contentState, DateTimeOffset now)
    {
        var aps = new JsonObject
        {
            ["timestamp"] = now.ToUnixTimeSeconds(),
            ["event"] = "end",
            ["dismissal-date"] = now.ToUnixTimeSeconds(),
            ["content-state"] = JsonSerializer.SerializeToNode(contentState, serializerOptions)
        };

        return new JsonObject { ["aps"] = aps }.ToJsonString(serializerOptions);
    }

    private static double toUnixSeconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds() / 1000.0;
}
