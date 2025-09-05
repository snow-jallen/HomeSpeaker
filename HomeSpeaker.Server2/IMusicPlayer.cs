using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public interface IMusicPlayer : IDisposable
{
    void PlaySong(Song song);
    void PlayStream(string streamUrl);
    bool StillPlaying { get; }
    void EnqueueSong(Song song);
    PlayerStatus Status { get; }
    IEnumerable<Song> SongQueue { get; }
    void ClearQueue();
    void ResumePlay();
    void SkipToNext();
    void Stop();
    void SetVolume(int level0to100);
    Task<int> GetVolume();
    void ShuffleQueue();
    void UpdateQueue(IEnumerable<string> songs);

    event EventHandler<string> PlayerEvent;
}
