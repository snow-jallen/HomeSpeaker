namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>How many of one person's anchors are done today.</summary>
public sealed record AnchorProgress(string UserId, int Done, int Total)
{
    public bool IsComplete => Total > 0 && Done >= Total;

    /// <summary>0 to 1, for the progress bar.</summary>
    public double Fraction => Total == 0 ? 0 : Math.Clamp((double)Done / Total, 0, 1);
}
