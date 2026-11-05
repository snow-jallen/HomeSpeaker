namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>An <see cref="AnchorResult"/> plus the created or changed item when it worked.</summary>
public sealed record AnchorOutcome<T>(AnchorResult Result, T? Value = null, string? Message = null)
    where T : class
{
    public bool Succeeded => Result == AnchorResult.Ok;
}
