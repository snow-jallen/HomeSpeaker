using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Models;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

// Simple replacement for gRPC types
public class GetStatusReply
{
    public int Volume { get; set; }
    public bool StilPlaying { get; set; }
    public SongMessage? CurrentSong { get; set; }
}

public class SongMessage
{
    public int SongId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

/// <summary>
/// Server-side service for Blazor SSR components that wraps backend music player functionality.
/// Provides the same API as the old gRPC client wrapper but calls backend services directly.
/// </summary>
public class HomeSpeakerService
{
    private readonly ILogger<HomeSpeakerService> logger;
    private readonly Mp3Library library;
    private readonly IMusicPlayer musicPlayer;
    private readonly YoutubeService youtubeService;
    private readonly PlaylistService playlistService;
    private readonly RadioStreamService radioStreamService;
    private readonly IServiceProvider serviceProvider;

    public event EventHandler? QueueChanged;
    public event Action<string>? StatusChanged;

    public HomeSpeakerService(
        ILogger<HomeSpeakerService> logger,
        Mp3Library library,
        IMusicPlayer musicPlayer,
        YoutubeService youtubeService,
        PlaylistService playlistService,
        RadioStreamService radioStreamService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.library = library ?? throw new ArgumentNullException(nameof(library));
        this.musicPlayer = musicPlayer ?? throw new ArgumentNullException(nameof(musicPlayer));
        this.youtubeService = youtubeService;
        this.playlistService = playlistService;
        this.radioStreamService = radioStreamService;
        this.serviceProvider = serviceProvider;

        // Forward player events
        this.musicPlayer.PlayerEvent += (sender, msg) =>
        {
            StatusChanged?.Invoke(msg);
        };
    }

    // Volume
    public async Task SetVolumeAsync(int volume0to100)
    {
        await Task.Run(() => musicPlayer.SetVolume(volume0to100));
    }

    public async Task<int> GetVolumeAsync()
    {
        return await musicPlayer.GetVolume();
    }

    // Player status - returns gRPC-compatible reply for components
    public async Task<GetStatusReply> GetStatusAsync()
    {
        var status = musicPlayer.Status;
        var currentSong = status?.CurrentSong;

        return await Task.FromResult(new GetStatusReply
        {
            Volume = await musicPlayer.GetVolume(),
            StilPlaying = musicPlayer.StillPlaying,
            CurrentSong = currentSong != null ? new SongMessage
            {
                SongId = currentSong.SongId,
                Path = currentSong.Path,
                Name = currentSong.Name,
                Artist = currentSong.Artist ?? string.Empty,
                Album = currentSong.Album ?? string.Empty,
                Duration = currentSong.Duration?.ToString() ?? string.Empty
            } : null
        });
    }

    // Queue management
    public async Task UpdateQueueAsync(List<SongViewModel> songs)
    {
        await Task.Run(() =>
        {
            musicPlayer.ClearQueue();
            foreach (var song in songs)
            {
                var fullSong = library.Songs.FirstOrDefault(s => s.Path == song.Path);
                if (fullSong != null)
                {
                    musicPlayer.EnqueueSong(fullSong);
                }
            }
        });
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IEnumerable<SongViewModel>> GetPlayQueueAsync()
    {
        return await Task.FromResult(musicPlayer.SongQueue.Select(s => s.ToSongViewModel()));
    }

    public async Task ClearQueueAsync()
    {
        await Task.Run(() => musicPlayer.ClearQueue());
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ShuffleQueueAsync()
    {
        await Task.Run(() => musicPlayer.ShuffleQueue());
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    // Playback control
    public async Task PlaySongAsync(int songId)
    {
        await Task.Run(() =>
        {
            musicPlayer.Stop();
            musicPlayer.ClearQueue();
            var song = library.Songs.FirstOrDefault(s => s.SongId == songId);
            if (song != null)
            {
                musicPlayer.EnqueueSong(song);
            }
        });
    }

    public async Task EnqueueSongAsync(int songId)
    {
        await Task.Run(() =>
        {
            var song = library.Songs.FirstOrDefault(s => s.SongId == songId);
            if (song != null)
            {
                musicPlayer.EnqueueSong(song);
            }
        });
    }

    public async Task PlayFolderAsync(string folder)
    {
        await Task.Run(() =>
        {
            musicPlayer.Stop();
            musicPlayer.ClearQueue();
            var songs = library.Songs.Where(s => s.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }
        });
    }

    public async Task EnqueueFolderAsync(string folder)
    {
        await Task.Run(() =>
        {
            var songs = library.Songs.Where(s => s.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }
        });
    }

    public async Task EnqueueFolderAsync(SongGroup songGroup) =>
        await EnqueueFolderAsync(songGroup.FolderPath);

    public async Task StopPlayingAsync()
    {
        await Task.Run(() => musicPlayer.Stop());
    }

    public async Task ResumePlayAsync()
    {
        await Task.Run(() =>
        {
            if (!musicPlayer.StillPlaying && musicPlayer.SongQueue.Any())
            {
                musicPlayer.ResumePlay();
            }
        });
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SkipToNextAsync()
    {
        await Task.Run(() => musicPlayer.SkipToNext());
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    // Song library
    public async Task<IEnumerable<SongViewModel>> GetAllSongsAsync()
    {
        return await Task.Run(() => library.Songs.Select(s => s.ToSongViewModel()));
    }

    public async Task<IEnumerable<SongViewModel>> GetSongsInFolder(string folder)
    {
        return await Task.Run(() =>
            library.Songs
                .Where(s => s.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ToSongViewModel()));
    }

    public async Task<Dictionary<string, List<SongViewModel>>> GetSongGroups()
    {
        return await Task.Run(() =>
        {
            var groups = new Dictionary<string, List<SongViewModel>>();
            foreach (var song in library.Songs)
            {
                var folder = GetFolderFromPath(song.Path);
                if (string.IsNullOrEmpty(folder))
                    continue;

                if (!groups.ContainsKey(folder))
                {
                    groups[folder] = new List<SongViewModel>();
                }

                groups[folder].Add(song.ToSongViewModel());
            }
            return groups;
        });
    }

    public async Task<IEnumerable<string>> GetFolders()
    {
        return await Task.Run(() =>
        {
            var folders = new HashSet<string>();
            foreach (var song in library.Songs)
            {
                var folder = GetTopLevelFolder(song.Path);
                if (!string.IsNullOrEmpty(folder))
                {
                    folders.Add(folder);
                }
            }
            return folders.AsEnumerable();
        });
    }

    private string? GetFolderFromPath(string path)
    {
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : null;
    }

    private string? GetTopLevelFolder(string path)
    {
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    public async Task UpdateSongAsync(int songId, string name, string artist, string album)
    {
        library.UpdateSong(songId, name, artist, album);
        await Task.CompletedTask;
    }

    // Playlists
    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync()
    {
        return await playlistService.GetPlaylistsAsync();
    }

    public async Task AddToPlaylistAsync(string playlistName, string songPath)
    {
        await playlistService.AddSongToPlaylistAsync(playlistName, songPath);
    }

    public async Task RemoveFromPlaylistAsync(string playlistName, string songPath)
    {
        await playlistService.RemoveSongFromPlaylistAsync(playlistName, songPath);
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        await playlistService.PlayPlaylistAsync(playlistName, musicPlayer);
    }

    public async Task RenamePlaylistAsync(string oldName, string newName)
    {
        logger.LogInformation("Renaming playlist: {OldName} -> {NewName}", oldName, newName);
        await playlistService.RenamePlaylistAsync(oldName, newName);
    }

    public async Task DeletePlaylistAsync(string playlistName)
    {
        await playlistService.DeletePlaylistAsync(playlistName);
    }

    public async Task ReorderPlaylistSongsAsync(string playlistName, List<string> songPaths)
    {
        logger.LogInformation("Reordering playlist songs: {PlaylistName}", playlistName);
        await playlistService.ReorderPlaylistSongsAsync(playlistName, songPaths);
    }

    // Radio streams
    public async Task<IEnumerable<RadioStreamViewModel>> GetRadioStreamsAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
        var streams = await db.RadioStreams
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return streams.Select(s => new RadioStreamViewModel
        {
            Id = s.Id,
            Name = s.Name,
            Url = s.Url,
            FaviconFileName = string.IsNullOrWhiteSpace(s.FaviconFileName) ? null : s.FaviconFileName,
            PlayCount = s.PlayCount,
            DisplayOrder = s.DisplayOrder
        });
    }

    public async Task PlayRadioStreamAsync(int streamId)
    {
        await radioStreamService.PlayRadioStreamAsync(streamId, musicPlayer);
    }

    public async Task PlayStreamAsync(string streamUri)
    {
        await Task.Run(() =>
        {
            musicPlayer.Stop();
            musicPlayer.ClearQueue();
            musicPlayer.PlayStream(streamUri);
        });
    }

    public async Task<RadioStreamViewModel> CreateRadioStreamAsync(string name, string url, string faviconUrl = "", string faviconFileName = "")
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();

        var stream = new RadioStream
        {
            Name = name,
            Url = url,
            FaviconFileName = faviconFileName,
            DisplayOrder = await db.RadioStreams.MaxAsync(s => (int?)s.DisplayOrder) ?? 0 + 1
        };

        db.RadioStreams.Add(stream);
        await db.SaveChangesAsync();

        return new RadioStreamViewModel
        {
            Id = stream.Id,
            Name = stream.Name,
            Url = stream.Url,
            FaviconFileName = string.IsNullOrWhiteSpace(stream.FaviconFileName) ? null : stream.FaviconFileName,
            PlayCount = stream.PlayCount,
            DisplayOrder = stream.DisplayOrder
        };
    }

    public async Task<RadioStreamViewModel> UpdateRadioStreamAsync(int streamId, string name, string url, string faviconUrl = "", string faviconFileName = "")
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();

        var stream = await db.RadioStreams.FindAsync(streamId);
        if (stream == null)
        {
            throw new InvalidOperationException($"Radio stream {streamId} not found");
        }

        stream.Name = name;
        stream.Url = url;
        if (!string.IsNullOrEmpty(faviconFileName))
        {
            stream.FaviconFileName = faviconFileName;
        }

        await db.SaveChangesAsync();

        return new RadioStreamViewModel
        {
            Id = stream.Id,
            Name = stream.Name,
            Url = stream.Url,
            FaviconFileName = string.IsNullOrWhiteSpace(stream.FaviconFileName) ? null : stream.FaviconFileName,
            PlayCount = stream.PlayCount,
            DisplayOrder = stream.DisplayOrder
        };
    }

    // Repeat mode
    public async Task SetRepeatModeAsync(bool enabled)
    {
        await Task.Run(() => musicPlayer.RepeatMode = enabled);
    }

    public async Task<bool> GetRepeatModeAsync()
    {
        return await Task.FromResult(musicPlayer.RepeatMode);
    }

    // Sleep timer
    public async Task SetSleepTimerAsync(int minutes)
    {
        await Task.Run(() => musicPlayer.SetSleepTimer(minutes));
    }

    public async Task CancelSleepTimerAsync()
    {
        await Task.Run(() => musicPlayer.CancelSleepTimer());
    }

    public async Task<int?> GetSleepTimerAsync()
    {
        var remaining = musicPlayer.SleepTimerRemaining;
        return await Task.FromResult(remaining.HasValue ? (int?)remaining.Value.TotalMinutes : null);
    }

    // Backlight toggle
    public async Task ToggleBrightness()
    {
        logger.LogWarning("ToggleBrightness called but not implemented in SSR mode");
        await Task.CompletedTask;
    }
}
