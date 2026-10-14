using System.Text.Json;
using HomeSpeaker.Server2.Endpoints;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests;

public sealed class ServerSentEventFormatterTests
{
    [Fact]
    public void FormatsEventNameDataAndTerminatingBlankLine()
    {
        var frame = ServerSentEventFormatter.Format("status", "{\"volume\":40}");

        Assert.Equal("event: status\ndata: {\"volume\":40}\n\n", frame);
    }

    [Fact]
    public void SplitsMultiLineDataIntoSeveralDataLines()
    {
        var frame = ServerSentEventFormatter.Format("status", "line one\r\nline two");

        Assert.Equal("event: status\ndata: line one\ndata: line two\n\n", frame);
    }

    [Fact]
    public void PlayerStatusJsonIsASingleCamelCaseDataLine()
    {
        var status = new PlayerStatusDto
        {
            Elapsed = TimeSpan.FromSeconds(91.75),
            StillPlaying = true,
            CurrentSong = new Song { SongId = 3, Name = "Two\nLines" },
            IsStream = true,
            StreamName = "KUSC"
        };
        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var frame = ServerSentEventFormatter.Format("status", json);

        var lines = frame.Split('\n');
        Assert.Equal(["event: status", $"data: {json}", string.Empty, string.Empty], lines);
        Assert.Contains("\"elapsed\":\"00:01:31.7500000\"", json, StringComparison.Ordinal);
        Assert.Contains("\"isStream\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"streamName\":\"KUSC\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sleepTimerRemainingMinutes\":null", json, StringComparison.Ordinal);
    }
}
