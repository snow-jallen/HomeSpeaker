using HomeSpeaker.WebAssembly.Models;

namespace HomeSpeaker.WebAssembly.Services;

public interface ILocalQueueService
{
    event EventHandler? QueueChanged;
    event EventHandler<SongViewModel>? CurrentSongChanged;
    
    IReadOnlyList<SongViewModel> Queue { get; }
    SongViewModel? CurrentSong { get; }
    int CurrentIndex { get; }
    
    Task AddSongAsync(SongViewModel song);
    Task AddSongsAsync(IEnumerable<SongViewModel> songs);
    Task PlaySongAsync(SongViewModel song);
    Task PlaySongsAsync(IEnumerable<SongViewModel> songs);
    Task RemoveSongAsync(int index);
    Task ClearQueueAsync();
    Task MoveSongAsync(int fromIndex, int toIndex);
    Task PlayNextAsync();
    Task PlayPreviousAsync();
    Task PlaySongAtIndexAsync(int index);
    Task ShuffleQueueAsync();
}

public class LocalQueueService : ILocalQueueService
{
    private readonly IBrowserAudioService audioService;
    private readonly ILogger<LocalQueueService> logger;
    private readonly List<SongViewModel> queue = new();
    private int currentIndex = -1;

    public event EventHandler? QueueChanged;
    public event EventHandler<SongViewModel>? CurrentSongChanged;

    public IReadOnlyList<SongViewModel> Queue => queue.AsReadOnly();
    public SongViewModel? CurrentSong => currentIndex >= 0 && currentIndex < queue.Count ? queue[currentIndex] : null;
    public int CurrentIndex => currentIndex;

    public LocalQueueService(IBrowserAudioService audioService, ILogger<LocalQueueService> logger)
    {
        this.audioService = audioService;
        this.logger = logger;
        
        // Subscribe to audio service events to handle song completion
        this.audioService.StatusChanged += OnAudioStatusChanged;
    }

    public async Task AddSongAsync(SongViewModel song)
    {
        queue.Add(song);
    logger.LogInformation("Added song {SongName} to local queue. Queue now has {Count} songs", song.Name, queue.Count);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddSongsAsync(IEnumerable<SongViewModel> songs)
    {
        var songList = songs.ToList();
        queue.AddRange(songList);
    logger.LogInformation("Added {Count} songs to local queue. Queue now has {TotalCount} songs", songList.Count, queue.Count);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlaySongAsync(SongViewModel song)
    {
        await ClearQueueAsync();
        await AddSongAsync(song);
        await PlaySongAtIndexAsync(0);
    }

    public async Task PlaySongsAsync(IEnumerable<SongViewModel> songs)
    {
        await ClearQueueAsync();
        await AddSongsAsync(songs);
        if (queue.Count > 0)
        {
            await PlaySongAtIndexAsync(0);
        }
    }

    public async Task RemoveSongAsync(int index)
    {
        if (index < 0 || index >= queue.Count)
            return;

        var removedSong = queue[index];
        queue.RemoveAt(index);
        
        // Adjust current index if necessary
        if (index < currentIndex)
        {
            currentIndex--;
        }
        else if (index == currentIndex)
        {
            // The currently playing song was removed
            if (currentIndex >= queue.Count)
            {
                currentIndex = queue.Count - 1;
            }
            
            // If there are still songs, play the next one
            if (queue.Count > 0 && currentIndex >= 0)
            {
                await PlaySongAtIndexAsync(currentIndex);
            }
            else
            {
                await audioService.StopAsync();
                currentIndex = -1;
                CurrentSongChanged?.Invoke(this, null!);
            }
        }
        
    logger.LogInformation("Removed song {SongName} from local queue at index {Index}", removedSong.Name, index);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearQueueAsync()
    {
        queue.Clear();
        currentIndex = -1;
        await audioService.StopAsync();
    logger.LogInformation("Cleared local queue");
        QueueChanged?.Invoke(this, EventArgs.Empty);
        CurrentSongChanged?.Invoke(this, null!);
    }

    public async Task MoveSongAsync(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= queue.Count || toIndex < 0 || toIndex >= queue.Count)
            return;

        var song = queue[fromIndex];
        queue.RemoveAt(fromIndex);
        queue.Insert(toIndex, song);

        // Adjust current index if necessary
        if (fromIndex == currentIndex)
        {
            currentIndex = toIndex;
        }
        else if (fromIndex < currentIndex && toIndex >= currentIndex)
        {
            currentIndex--;
        }
        else if (fromIndex > currentIndex && toIndex <= currentIndex)
        {
            currentIndex++;
        }

    logger.LogInformation("Moved song {SongName} from index {FromIndex} to {ToIndex}", song.Name, fromIndex, toIndex);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlayNextAsync()
    {
        if (currentIndex + 1 < queue.Count)
        {
            await PlaySongAtIndexAsync(currentIndex + 1);
        }
        else
        {
            // End of queue reached
            await audioService.StopAsync();
            logger.LogInformation("Reached end of local queue");
        }
    }

    public async Task PlayPreviousAsync()
    {
        if (currentIndex > 0)
        {
            await PlaySongAtIndexAsync(currentIndex - 1);
        }
    }

    public async Task PlaySongAtIndexAsync(int index)
    {
        if (index < 0 || index >= queue.Count)
            return;

        currentIndex = index;
        var song = queue[currentIndex];
        
        try
        {
            await audioService.PlaySongAsync(song);
            logger.LogInformation("Playing song {SongName} at index {Index} from local queue", song.Name, index);
            CurrentSongChanged?.Invoke(this, song);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error playing song {SongName} from local queue", song.Name);
        }
    }

    private async void OnAudioStatusChanged(object? sender, BrowserPlayerStatus status)
    {
        // Check if the current song has ended
        if (status.Duration > 0 && Math.Abs(status.CurrentTime - status.Duration) < 1.0 && !status.IsPlaying)
        {
            logger.LogInformation("Song ended, automatically playing next song in local queue");
            await PlayNextAsync();
        }
    }

    public Task ShuffleQueueAsync()
    {
        if (queue.Count <= 1)
            return Task.CompletedTask;

        var currentSong = CurrentSong;
        var random = new Random();
        
        // Shuffle the queue using Fisher-Yates algorithm
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            var temp = queue[i];
            queue[i] = queue[j];
            queue[j] = temp;
        }

        // Update current index to point to the currently playing song if there is one
        if (currentSong != null)
        {
            currentIndex = queue.FindIndex(s => s.SongId == currentSong.SongId);
        }

    logger.LogInformation("Shuffled local queue with {Count} songs", queue.Count);
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
