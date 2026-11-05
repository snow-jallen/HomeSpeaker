namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>The anchor home (another HomeSpeaker) couldn't be reached and there is nothing cached to show instead.</summary>
public sealed class AnchorSourceUnavailableException : Exception
{
    public AnchorSourceUnavailableException()
    {
    }

    public AnchorSourceUnavailableException(string message)
        : base(message)
    {
    }

    public AnchorSourceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
