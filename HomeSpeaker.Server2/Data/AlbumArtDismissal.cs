namespace HomeSpeaker.Server2.Data;

/// <summary>A missing-art group the admin hid on the album art page. <see cref="Artist"/> and <see cref="Name"/> are for reading the table by hand.</summary>
public class AlbumArtDismissal
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DismissedUtc { get; set; }
}
