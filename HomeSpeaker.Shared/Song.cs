namespace HomeSpeaker.Shared;

public record Song
{
    public int SongId { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string Album { get; set; }
    public string Artist { get; set; }
}

public static class ProtobufExtensions
{
    public static PlayerStatus ToPlayerStatus(this GetStatusReply reply)
    {
        return new PlayerStatus
        {
            CurrentSong = reply.CurrentSong?.ToSong(),
            Elapsed = reply.Elapsed.ToTimeSpan(),
            PercentComplete = (decimal)reply.PercentComplete,
            Remaining = reply.Remaining.ToTimeSpan(),
            StillPlaying = reply.StilPlaying
        };
    }

    public static Song ToSong(this SongMessage song)
    {
        return new Song
        {
            SongId = song?.SongId ?? -1,
            Name = song?.Name ?? "[ Null Song Response ??? ]",
            Album = song?.Album,
            Artist = song?.Artist,
            Path = song?.Path
        };
    }
}
