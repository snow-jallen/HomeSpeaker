using System.Globalization;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// Geometry for the Home glucose trend line, in percent of the chart box (0,0 = top-left, 100,100 = bottom-right)
/// so the markup can stretch it to any size. The x axis is time (the last <see cref="Window"/> up to now), so
/// gaps in the data stay visible (a reading stamped slightly in the future, from a fast uploader clock, is pinned to
/// the right edge); the y axis is a fixed <see cref="YMin"/>-<see cref="YMax"/> mg/dL with outliers clamped.
/// </summary>
public sealed record GlucoseChart(
    IReadOnlyList<string> Paths,
    double BandTopPercent,
    double BandHeightPercent,
    GlucoseChart.ChartPoint Latest)
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(3);

    /// <summary>The line is broken wherever two neighbouring readings are further apart than this.</summary>
    public static readonly TimeSpan GapBreak = TimeSpan.FromMinutes(12);

    public const double YMin = 40;
    public const double YMax = 300;
    public const double TargetLow = 70;
    public const double TargetHigh = 180;

    public readonly record struct ChartPoint(double X, double Y);

    /// <summary>Builds the chart from readings in any order, or null when none fall inside the window.</summary>
    public static GlucoseChart? Build(IEnumerable<BloodSugarReading> readings, DateTime nowUtc)
    {
        var start = nowUtc - Window;
        var points = readings
            .Select(r => (Time: r.Date.ToUniversalTime(), r.Sgv))
            .Where(p => p.Time >= start)
            .OrderBy(p => p.Time)
            .ToList();

        if (points.Count == 0)
        {
            return null;
        }

        var paths = new List<string>();
        var segment = new List<ChartPoint>();
        DateTime? previousTime = null;
        foreach (var (time, sgv) in points)
        {
            if (previousTime is { } previous && time - previous > GapBreak)
            {
                paths.Add(toPath(segment));
                segment.Clear();
            }

            segment.Add(new ChartPoint(xPercent(time, start), yPercent(sgv)));
            previousTime = time;
        }

        paths.Add(toPath(segment));

        var bandTop = yPercent(TargetHigh);
        return new GlucoseChart(paths, bandTop, yPercent(TargetLow) - bandTop, new ChartPoint(xPercent(points[^1].Time, start), yPercent(points[^1].Sgv)));
    }

    private static double xPercent(DateTime time, DateTime start) =>
        Math.Clamp((time - start) / Window * 100, 0, 100);

    private static double yPercent(double sgv) =>
        (YMax - Math.Clamp(sgv, YMin, YMax)) / (YMax - YMin) * 100;

    // A lone point is drawn as a zero-length segment so a round line cap still shows it as a dot.
    private static string toPath(List<ChartPoint> segment)
    {
        var parts = segment.Select((p, i) => string.Create(CultureInfo.InvariantCulture, $"{(i == 0 ? 'M' : 'L')}{p.X:0.##} {p.Y:0.##}"));
        var path = string.Join(' ', parts);
        return segment.Count == 1 ? path + string.Create(CultureInfo.InvariantCulture, $" L{segment[0].X:0.##} {segment[0].Y:0.##}") : path;
    }
}
