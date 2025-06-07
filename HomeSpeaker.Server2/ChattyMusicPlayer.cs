using HomeSpeaker.Shared;

namespace HomeSpeaker.Server;

public class ChattyMusicPlayer : IMusicPlayer
{
    private readonly IMusicPlayer actualPlayer;

    public ChattyMusicPlayer(IMusicPlayer actualPlayer)
    {
        this.actualPlayer = actualPlayer;
        actualPlayer.PlayerEvent += (_, msg) => PlayerEvent?.Invoke(this, msg);
    }

    public bool StillPlaying => actualPlayer.StillPlaying;

    public PlayerStatus Status => actualPlayer.Status;

    public IEnumerable<Song> SongQueue => actualPlayer.SongQueue;

    public event EventHandler<string>? PlayerEvent;

    public void ClearQueue()
    {
        actualPlayer.ClearQueue();
        PlayerEvent?.Invoke(this, "Cleared queue");
    }

    public void EnqueueSong(Song song)
    {
        actualPlayer.EnqueueSong(song);
        PlayerEvent?.Invoke(this, "Queued up: " + song.Name);
    }

    public Task<int> GetVolume()
    {
        return actualPlayer.GetVolume();
    }

    public void PlaySong(Song song, float startTime = 0)
    {
        actualPlayer.PlaySong(song, startTime);
        PlayerEvent?.Invoke(this, "Played: " + song.Name);
    }

    public void PlayStream(string streamUrl)
    {
        actualPlayer.PlayStream(streamUrl);
        PlayerEvent?.Invoke(this, "Played stream: " + streamUrl);
    }

    public void ResumePlay()
    {
        actualPlayer.ResumePlay();
        PlayerEvent?.Invoke(this, "Resumed play.");
    }

    public void SetVolume(int level0to100) => actualPlayer.SetVolume(level0to100);

    public void ShuffleQueue()
    {
        actualPlayer.ShuffleQueue();
        PlayerEvent?.Invoke(this, "Shuffled queue.");
    }

    public void SkipToNext()
    {
        actualPlayer.SkipToNext();
        PlayerEvent?.Invoke(this, "Skipped to next");
    }

    public void Stop()
    {
        actualPlayer.Stop();
        PlayerEvent?.Invoke(this, "Stopped playing.");
    }

    public void UpdateQueue(IEnumerable<string> songs)
    {
        actualPlayer.UpdateQueue(songs);
        PlayerEvent?.Invoke(this, "Updated queue.");
    }
}
