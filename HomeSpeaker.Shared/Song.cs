namespace HomeSpeaker.Shared;

public record Song
{
    public int SongId { get; set; }
    public string Name { get; set; }
    public string? Path { get; set; }
    public string? Album { get; set; }
    public string? Artist { get; set; }
}
