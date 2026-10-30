namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>HttpClient names and the User-Agent the album art lookups send (MusicBrainz requires one with contact details).</summary>
public static class AlbumArtHttp
{
    public const string LookupClientName = "AlbumArtLookup";
    public const string DownloadClientName = "AlbumArtDownload";
    public const string UserAgent = "HomeSpeaker/1.0 ( https://github.com/snow-jallen/homespeaker )";
}
