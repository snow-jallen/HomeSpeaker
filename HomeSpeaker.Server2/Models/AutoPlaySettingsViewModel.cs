namespace HomeSpeaker.Server2.Models;

public sealed class AutoPlaySettingsViewModel
{
    public int VolumeLevel { get; set; } = 30;
    public int SilenceTimeoutMinutes { get; set; } = 30;
    public List<AutoPlaySourceViewModel> Sources { get; set; } = new();
}

public sealed class AutoPlaySourceViewModel
{
    public int Id { get; set; }
    public AutoPlaySourceType SourceType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PlaylistName { get; set; }
    public int? RadioStreamId { get; set; }
}
