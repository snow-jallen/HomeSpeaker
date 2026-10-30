namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>The chosen image could not be used; the message is shown to the admin as is.</summary>
public sealed class AlbumArtDownloadException : Exception
{
    public AlbumArtDownloadException()
    {
    }

    public AlbumArtDownloadException(string message)
        : base(message)
    {
    }

    public AlbumArtDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
