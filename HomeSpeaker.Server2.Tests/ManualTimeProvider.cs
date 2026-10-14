namespace HomeSpeaker.Server2.Tests;

/// <summary>A clock that only moves when a test advances it.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now;

    public ManualTimeProvider(DateTimeOffset start) => now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
