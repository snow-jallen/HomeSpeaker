using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests;

internal sealed class RecordingMusicPlayer : IMusicPlayer
{
    public List<Song> Played { get; } = [];

    public List<Song> Enqueued { get; } = [];

    public int StopCount { get; private set; }

    public bool StillPlaying => Played.Count > 0;

    public PlayerStatus Status => new() { StillPlaying = StillPlaying, CurrentSong = Played.LastOrDefault() };

    public IEnumerable<Song> SongQueue => Enqueued;

    public bool RepeatMode { get; set; }

    public bool SleepTimerActive => false;

    public TimeSpan? SleepTimerRemaining => null;

    public event EventHandler<string>? PlayerEvent
    {
        add { }
        remove { }
    }

    public void PlaySong(Song song) => Played.Add(song);

    public void EnqueueSong(Song song) => Enqueued.Add(song);

    public void PlayStream(string streamUrl, string? name = null) => throw new NotSupportedException();

    public void ClearQueue() => Enqueued.Clear();

    public void ResumePlay() => throw new NotSupportedException();

    public void SkipToNext() => throw new NotSupportedException();

    public void Stop() => StopCount++;

    public void SetVolume(int level0to100) => throw new NotSupportedException();

    public Task<int> GetVolume() => throw new NotSupportedException();

    public void ShuffleQueue() => throw new NotSupportedException();

    public void UpdateQueue(IEnumerable<string> songs) => throw new NotSupportedException();

    public void SetSleepTimer(int minutes) => throw new NotSupportedException();

    public void CancelSleepTimer() => throw new NotSupportedException();

    public void Dispose()
    {
    }
}
