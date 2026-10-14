using System.Text;

namespace HomeSpeaker.Server2.Endpoints;

/// <summary>Formats Server-Sent Events frames (<c>text/event-stream</c>).</summary>
public static class ServerSentEventFormatter
{
    /// <summary>
    /// Formats one event: an <c>event:</c> line, one <c>data:</c> line per line of
    /// <paramref name="data"/> (compact JSON is a single line), and the blank line that ends it.
    /// </summary>
    public static string Format(string eventName, string data)
    {
        var frame = new StringBuilder();
        frame.Append("event: ").Append(eventName).Append('\n');
        foreach (var line in data.ReplaceLineEndings("\n").Split('\n'))
        {
            frame.Append("data: ").Append(line).Append('\n');
        }

        frame.Append('\n');
        return frame.ToString();
    }
}
