namespace HomeSpeaker.Server2.Services;

public class YouTubeStateService
{
    public string? SearchTerm { get; set; }
    public IEnumerable<VideoDto>? Videos { get; set; }
}
