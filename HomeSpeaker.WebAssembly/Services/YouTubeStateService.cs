using HomeSpeaker.Shared;

namespace HomeSpeaker.WebAssembly.Services;

public class YouTubeStateService
{
    public string? SearchTerm { get; set; }
    public IEnumerable<Video>? Videos { get; set; }
}
