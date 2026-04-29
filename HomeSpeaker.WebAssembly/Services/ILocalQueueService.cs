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
