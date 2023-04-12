using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Server;

public class HomeSpeakerService : HomeSpeakerBase
{
    private readonly ILogger<HomeSpeakerService> logger;
    private readonly Mp3Library library;
    private readonly IMusicPlayer musicPlayer;
    private readonly YoutubeService youtubeService;
    private readonly List<IServerStreamWriter<StreamServerEvent>> eventClients = new();
    private readonly List<IServerStreamWriter<StreamServerEvent>> failedEvents = new();

    public HomeSpeakerService(ILogger<HomeSpeakerService> logger, Mp3Library library, IMusicPlayer musicPlayer, YoutubeService youtubeService)
    {
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        this.library = library ?? throw new System.ArgumentNullException(nameof(library));
        this.musicPlayer = musicPlayer ?? throw new System.ArgumentNullException(nameof(musicPlayer));
        this.youtubeService = youtubeService;
        musicPlayer.PlayerEvent += MusicPlayer_PlayerEvent;
    }

    private async void MusicPlayer_PlayerEvent(object? sender, string message)
    {
        foreach (var client in eventClients)
        {
            try
            {
                await client.WriteAsync(new StreamServerEvent { Message = message });
            }
            catch
            {
                failedEvents.Add(client);
            }
        }

        if (failedEvents.Any())
        {
            foreach (var client in failedEvents)
            {
                eventClients.Remove(client);
            }
            failedEvents.Clear();
        }
    }

    public override Task<DeleteSongReply> DeleteSong(DeleteSongRequest request, ServerCallContext context)
    {
        library.DeleteSong(request.SongId);
        return Task.FromResult(new DeleteSongReply());
    }

    public override async Task<SearchVideoReply> SearchViedo(SearchVideoRequest request, ServerCallContext context)
    {
        var videos = await youtubeService.SearchAsync(request.SearchTerm);
        var result = new SearchVideoReply();
        result.Results.AddRange(videos.Select(v => new Shared.Video
        {
            Title = v.Title,
            Id = v.Id,
            Url = v.Url,
            Thumbnail = v.Thumbnail,
            Author = v.Author,
            Duration = Duration.FromTimeSpan(v.Duration ?? TimeSpan.Zero)
        }));
        return result;
    }

    public override async Task CacheVideo(CacheVideoRequest request, IServerStreamWriter<CacheVideoReply> responseStream, ServerCallContext context)
    {
        var v = request.Video;
        var streamingProgress = new StreamingProgress(responseStream, v.Title, logger);
        await youtubeService.CacheVideoAsync(v.Id, v.Title, streamingProgress);
        library.IsDirty = true;
    }

    public override async Task GetSongs(GetSongsRequest request, IServerStreamWriter<GetSongsReply> responseStream, ServerCallContext context)
    {
        var reply = new GetSongsReply();
        if (library?.Songs?.Any() ?? false)
        {
            IEnumerable<Song> songs = library.Songs;
            if (!string.IsNullOrEmpty(request.Folder))
            {
                logger.LogInformation("Filtering songs to just those in the {folder} folder", request.Folder);
                songs = songs.Where(s => s.Path.Contains(request.Folder));
            }
            logger.LogInformation("Found songs!  Sending to client.");
            var songMessages = translateSongs(songs);
            reply.Songs.AddRange(songMessages);
        }
        else
        {
            logger.LogInformation("No songs found.  Sending back empty list.");
        }
        await responseStream.WriteAsync(reply);
    }

    public override Task<PlaySongReply> PlaySong(PlaySongRequest request, ServerCallContext context)
    {
        logger.LogInformation("PlaySong request for {songid}", request.SongId);

        var song = library.Songs.FirstOrDefault(s => s.SongId == request.SongId);

        var reply = new PlaySongReply { Ok = false };
        if (song != null)
        {
            _ = Task.Run(() =>
                musicPlayer.PlaySong(song)
            );
            reply.Ok = true;
        }
        else
        {
            logger.LogWarning("Song {songid} not found in library.", request.SongId);
        }
        return Task.FromResult(reply);
    }

    public override Task<PlaySongReply> PlayStream(PlayStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("PlayStream request for {streamurl}", request.StreamUrl);
        musicPlayer.PlayStream(request.StreamUrl);
        return Task.FromResult(new PlaySongReply { Ok = true });
    }

    public override Task<PlaySongReply> EnqueueSong(PlaySongRequest request, ServerCallContext context)
    {
        logger.LogInformation("EnqueueSong request for {songid}", request.SongId);

        var song = library.Songs.FirstOrDefault(s => s.SongId == request.SongId);
        var reply = new PlaySongReply { Ok = false };
        if (song != null)
        {
            logger.LogInformation($"Queuing up #{song.SongId}: {song.Name}");
            musicPlayer.EnqueueSong(song);
            reply.Ok = true;
        }
        else
        {
            logger.LogWarning("Song {songid} not found in library", request.SongId);
        }

        return Task.FromResult(reply);
    }

    public override Task<GetStatusReply> GetPlayerStatus(GetStatusRequest request, ServerCallContext context)
    {
        var status = musicPlayer?.Status ?? new Shared.PlayerStatus();
        return Task.FromResult(new GetStatusReply
        {
            Elapsed = Duration.FromTimeSpan(status.Elapsed),
            PercentComplete = (double)status.PercentComplete,
            Remaining = Duration.FromTimeSpan(status.Remaining),
            StilPlaying = status.StillPlaying,
            CurrentSong = status.CurrentSong != null ? translateSong(status.CurrentSong) : null
        });
    }

    public override async Task GetPlayQueue(GetSongsRequest request, IServerStreamWriter<GetSongsReply> responseStream, ServerCallContext context)
    {
        var reply = new GetSongsReply();
        System.Collections.Generic.IEnumerable<Shared.Song> songQueue = musicPlayer.SongQueue;
        if (songQueue.Any())
        {
            logger.LogInformation("Found songs in queue!  Sending to client.");
            reply.Songs.AddRange(translateSongs(songQueue));
        }
        else
        {
            logger.LogInformation("No songs in queue.  Sending back empty list.");
        }
        await responseStream.WriteAsync(reply);
    }

    private IEnumerable<SongMessage> translateSongs(IEnumerable<Song> songQueue)
    {
        return songQueue.Select(translateSong);
    }

    private SongMessage translateSong(Song s)
    {
        string? path = s?.Path.Replace(library.RootFolder, string.Empty, StringComparison.InvariantCultureIgnoreCase).Substring(1);
        if (path == s?.Path)
        {
            logger.LogWarning("what? orig {orig} is same as {new}", s.Path, path);
        }
        return new SongMessage
        {
            Album = s?.Album ?? "[ No Album ]",
            Artist = s?.Artist ?? "[ No Artist ]",
            Name = s?.Name ?? Path.GetFileNameWithoutExtension(s?.Path),
            Path = path,
            SongId = s?.SongId ?? -1
        };
    }

    public override Task<PlayerControlReply> PlayerControl(PlayerControlRequest request, ServerCallContext context)
    {
        if (request.ClearQueue)
        {
            musicPlayer.ClearQueue();
        }
        if (request.Play)
        {
            musicPlayer.ResumePlay();
        }
        if (request.SkipToNext)
        {
            musicPlayer.SkipToNext();
        }
        if (request.Stop)
        {
            musicPlayer.Stop();
        }
        if (request.SetVolume)
        {
            musicPlayer.SetVolume(request.VolumeLevel);
        }
        return Task.FromResult(new PlayerControlReply());
    }

    public override Task<ShuffleQueueReply> ShuffleQueue(ShuffleQueueRequest request, ServerCallContext context)
    {
        musicPlayer.ShuffleQueue();
        return Task.FromResult(new ShuffleQueueReply());
    }

    public override Task<EnqueueFolderReply> EnqueueFolder(EnqueueFolderRequest request, ServerCallContext context)
    {
        foreach (var song in library.Songs.Where(s => s.Path.Contains(request.FolderPath)))
        {
            musicPlayer.EnqueueSong(song);
        }
        return Task.FromResult(new EnqueueFolderReply());
    }

    public override Task<PlayFolderReply> PlayFolder(PlayFolderRequest request, ServerCallContext context)
    {
        musicPlayer.Stop();
        foreach (var song in library.Songs.Where(s => s.Path.Contains(request.FolderPath)))
        {
            musicPlayer.EnqueueSong(song);
        }
        return Task.FromResult(new PlayFolderReply());
    }

    public override async Task SendEvent(Empty request, IServerStreamWriter<StreamServerEvent> responseStream, ServerCallContext context)
    {
        eventClients.Add(responseStream);
        await responseStream.WriteAsync(new StreamServerEvent { Message = "Client connected." });
        await Task.Delay(TimeSpan.FromMinutes(180));
    }
}
