using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Server2.Services;

public class HomeSpeakerService : HomeSpeakerBase
{
    private readonly ILogger<HomeSpeakerService> logger;
    private readonly Mp3Library library;
    private readonly IMusicPlayer musicPlayer;
    private readonly YoutubeService youtubeService;
    private readonly PlaylistService playlistService;
    private readonly RadioStreamService radioStreamService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly List<IServerStreamWriter<StreamServerEvent>> eventClients = new();
    private readonly List<IServerStreamWriter<StreamServerEvent>> failedEvents = new();

    public HomeSpeakerService(ILogger<HomeSpeakerService> logger, Mp3Library library, IMusicPlayer musicPlayer, YoutubeService youtubeService, PlaylistService playlistService, RadioStreamService radioStreamService, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
    {
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        this.library = library ?? throw new System.ArgumentNullException(nameof(library));
        this.musicPlayer = musicPlayer ?? throw new System.ArgumentNullException(nameof(musicPlayer));
        this.youtubeService = youtubeService;
        this.playlistService = playlistService;
        this.radioStreamService = radioStreamService;
        this.httpClientFactory = httpClientFactory;
        this.serviceProvider = serviceProvider;
        this.youtubeService = youtubeService;
        this.playlistService = playlistService;
        this.radioStreamService = radioStreamService;
        this.httpClientFactory = httpClientFactory;
        this.musicPlayer.PlayerEvent += musicPlayer_PlayerEvent;

        // Track song plays as impressions
        musicPlayer.PlayerEvent += async (sender, msg) =>
        {
            if (msg.StartsWith("Played: "))
            {
                var songName = msg.Substring(8);
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
                    db.Impressions.Add(new Impression
                    {
                        SongPath = musicPlayer.Status?.CurrentSong?.Path ?? "",
                        Timestamp = DateTime.UtcNow,
                        PlayedBy = "Server"
                    });
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error saving impression for {SongName}", songName);
                }
            }
        };
    }

    private readonly IServiceProvider serviceProvider;

    private async void musicPlayer_PlayerEvent(object? sender, string message)
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

    public override Task<UpdateQueueReply> UpdateQueue(UpdateQueueRequest request, ServerCallContext context)
    {
        musicPlayer.UpdateQueue(request.Songs);
        return Task.FromResult(new UpdateQueueReply());
    }

    public override async Task<PlayPlaylistReply> PlayPlaylist(PlayPlaylistRequest request, ServerCallContext context)
    {
        await playlistService.PlayPlaylistAsync(request.PlaylistName);
        return new PlayPlaylistReply();
    }

    public override async Task<RenamePlaylistReply> RenamePlaylist(RenamePlaylistRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received RenamePlaylist request: {OldName} -> {NewName}", request.OldName, request.NewName);
        await playlistService.RenamePlaylistAsync(request.OldName, request.NewName);
        logger.LogInformation("Successfully renamed playlist: {OldName} -> {NewName}", request.OldName, request.NewName);
        return new RenamePlaylistReply();
    }

    public override async Task<DeletePlaylistReply> DeletePlaylist(DeletePlaylistRequest request, ServerCallContext context)
    {
        await playlistService.DeletePlaylistAsync(request.PlaylistName);
        return new DeletePlaylistReply();
    }

    public override async Task<ReorderPlaylistSongsReply> ReorderPlaylistSongs(ReorderPlaylistSongsRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received ReorderPlaylistSongs request for playlist: {PlaylistName}", request.PlaylistName);
        await playlistService.ReorderPlaylistSongsAsync(request.PlaylistName, request.SongPaths.ToList());
        logger.LogInformation("Successfully reordered songs in playlist: {PlaylistName}", request.PlaylistName);
        return new ReorderPlaylistSongsReply();
    }

    public override async Task<ShufflePlaylistReply> ShufflePlaylist(ShufflePlaylistRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received ShufflePlaylist request for playlist: {playlistName}", request.PlaylistName);
        var shuffledPaths = await playlistService.ShufflePlaylistAsync(request.PlaylistName);
        logger.LogInformation("Successfully shuffled playlist: {playlistName}", request.PlaylistName);
        
        var reply = new ShufflePlaylistReply();
        reply.ShuffledSongPaths.AddRange(shuffledPaths);
        return reply;
    }

    public override async Task<SetPlaylistAlwaysShuffleReply> SetPlaylistAlwaysShuffle(SetPlaylistAlwaysShuffleRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received SetPlaylistAlwaysShuffle request for playlist: {playlistName}, alwaysShuffle: {alwaysShuffle}", 
            request.PlaylistName, request.AlwaysShuffle);
        await playlistService.SetPlaylistAlwaysShuffleAsync(request.PlaylistName, request.AlwaysShuffle);
        logger.LogInformation("Successfully set AlwaysShuffle for playlist: {playlistName}", request.PlaylistName);
        return new SetPlaylistAlwaysShuffleReply();
    }

    public override async Task<AddSongToPlaylistReply> AddSongToPlaylist(AddSongToPlaylistRequest request, ServerCallContext context)
    {
        await playlistService.AppendSongToPlaylistAsync(request.PlaylistName, request.SongPath);
        return new AddSongToPlaylistReply();
    }

    public override async Task<RemoveSongFromPlaylistReply> RemoveSongFromPlaylist(RemoveSongFromPlaylistRequest request, ServerCallContext context)
    {
        await playlistService.RemoveSongFromPlaylistAsync(request.PlaylistName, request.SongPath);
        return new RemoveSongFromPlaylistReply();
    }

    public override async Task<GetPlaylistsReply> GetPlaylists(GetPlaylistsRequest request, ServerCallContext context)
    {
        var reply = new GetPlaylistsReply();
        var playlists = await playlistService.GetPlaylistsAsync();
        foreach (var playlist in playlists)
        {
            var playlistMessage = new PlaylistMessage
            {
                PlaylistName = playlist.Name,
                AlwaysShuffle = playlist.AlwaysShuffle
            };
            var songs = playlist.Songs.Where(s => s != null);
            if (songs.Any())
            {
                playlistMessage.Songs.AddRange(translateSongs(songs));
            }

            reply.Playlists.Add(playlistMessage);
        }

        return reply;
    }

    public override Task<DeleteSongReply> DeleteSong(DeleteSongRequest request, ServerCallContext context)
    {
        library.DeleteSong(request.SongId);
        return Task.FromResult(new DeleteSongReply());
    }

    public override Task<UpdateSongReply> UpdateSong(UpdateSongRequest request, ServerCallContext context)
    {
        library.UpdateSong(request.SongId, request.Name, request.Artist, request.Album);
        return Task.FromResult(new UpdateSongReply());
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
            var songs = library.Songs;
            if (!string.IsNullOrEmpty(request.Folder))
            {
                logger.LogInformation("Filtering songs to just those in the {Folder} folder", request.Folder);
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
        logger.LogInformation("PlaySong request for {SongId}", request.SongId);

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
            logger.LogWarning("Song {SongId} not found in library.", request.SongId);
        }

        return Task.FromResult(reply);
    }

    public override Task<PlaySongReply> PlayStream(PlayStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("PlayStream request for {StreamUrl}", request.StreamUrl);
        musicPlayer.PlayStream(request.StreamUrl);
        return Task.FromResult(new PlaySongReply { Ok = true });
    }

    public override Task<PlaySongReply> EnqueueSong(PlaySongRequest request, ServerCallContext context)
    {
        logger.LogInformation("EnqueueSong request for {SongId}", request.SongId);

        var song = library.Songs.FirstOrDefault(s => s.SongId == request.SongId);
        var reply = new PlaySongReply { Ok = false };
        if (song != null)
        {
            logger.LogInformation("Queuing up #{SongId}: {SongName}", song.SongId, song.Name);
            musicPlayer.EnqueueSong(song);
            reply.Ok = true;
        }
        else
        {
            logger.LogWarning("Song {SongId} not found in library", request.SongId);
        }

        return Task.FromResult(reply);
    }

    public override async Task<GetStatusReply> GetPlayerStatus(GetStatusRequest request, ServerCallContext context)
    {
        var status = musicPlayer.Status ?? new Shared.PlayerStatus();
        var currentVolume = await musicPlayer.GetVolume();
        return new GetStatusReply
        {
            Elapsed = Duration.FromTimeSpan(status.Elapsed),
            PercentComplete = (double)status.PercentComplete,
            Remaining = Duration.FromTimeSpan(status.Remaining),
            StilPlaying = status.StillPlaying,
            CurrentSong = status.CurrentSong != null ? translateSong(status.CurrentSong) : null,
            Volume = currentVolume,
            IsStream = status.IsStream,
            StreamName = status.StreamName ?? string.Empty
        };
    }

    public override async Task GetPlayQueue(GetSongsRequest request, IServerStreamWriter<GetSongsReply> responseStream, ServerCallContext context)
    {
        var reply = new GetSongsReply();
        var songQueue = musicPlayer.SongQueue;
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
        //string? path = s?.Path.Replace(library.RootFolder, string.Empty, StringComparison.InvariantCultureIgnoreCase).Substring(1);
        //if (path == s?.Path)
        //{
        //    logger.LogWarning("what? orig {orig} is same as {new}", s.Path, path);
        //}
        return new SongMessage
        {
            Album = s?.Album ?? "[ No Album ]",
            Artist = s?.Artist ?? "[ No Artist ]",
            Name = s?.Name ?? Path.GetFileNameWithoutExtension(s?.Path),
            Path = s?.Path,
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

    public override async Task<Empty> ToggleBacklight(Empty request, ServerCallContext context)
    {
        var client = httpClientFactory.CreateClient("BacklightClient");

        var currentBrightnessStr = await client.GetStringAsync("/get");
        if (!int.TryParse(currentBrightnessStr, out var currentBrightness))
        {
            logger.LogWarning("Failed to parse brightness value: {Value}", currentBrightnessStr);
            return new Empty();
        }

        var newBrightness = currentBrightness switch
        {
            > 200 => 20,
            _ => 255
        };
        logger.LogInformation("Trying to set brightness to {Brightness}", newBrightness);
        var response = await client.GetAsync($"/set?brightness={newBrightness}");
        logger.LogInformation("response: {Response}", response);

        return new Empty();
    }

    public override async Task<GetRadioStreamsReply> GetRadioStreams(GetRadioStreamsRequest request, ServerCallContext context)
    {
        var streams = await radioStreamService.GetAllStreamsAsync();
        var reply = new GetRadioStreamsReply();

        foreach (var stream in streams)
        {
            reply.Streams.Add(new RadioStreamMessage
            {
                Id = stream.Id,
                Name = stream.Name,
                Url = stream.Url,
                FaviconFileName = stream.FaviconFileName ?? string.Empty,
                PlayCount = stream.PlayCount,
                DisplayOrder = stream.DisplayOrder,
                CreatedAt = Timestamp.FromDateTime(stream.CreatedAt.ToUniversalTime()),
                LastPlayedAt = stream.LastPlayedAt.HasValue
                    ? Timestamp.FromDateTime(stream.LastPlayedAt.Value.ToUniversalTime())
                    : null
            });
        }

        return reply;
    }

    public override async Task<PlayRadioStreamReply> PlayRadioStream(PlayRadioStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("PlayRadioStream request for stream ID {StreamId}", request.StreamId);

        var stream = await radioStreamService.GetStreamByIdAsync(request.StreamId);
        if (stream == null)
        {
            logger.LogWarning("Stream {StreamId} not found", request.StreamId);
            return new PlayRadioStreamReply { Ok = false };
        }

        // Increment play count
        await radioStreamService.IncrementPlayCountAsync(request.StreamId);

        // Play the stream
        musicPlayer.PlayStream(stream.Url, stream.Name);

        return new PlayRadioStreamReply { Ok = true };
    }

    public override async Task<RadioStreamMessage> CreateRadioStream(CreateRadioStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("CreateRadioStream request for {Name}", request.Name);

        var stream = await radioStreamService.CreateStreamAsync(
            request.Name,
            request.Url,
            string.IsNullOrWhiteSpace(request.FaviconUrl) ? null : request.FaviconUrl,
            string.IsNullOrWhiteSpace(request.FaviconFileName) ? null : request.FaviconFileName
        );

        return new RadioStreamMessage
        {
            Id = stream.Id,
            Name = stream.Name,
            Url = stream.Url,
            FaviconFileName = stream.FaviconFileName ?? string.Empty,
            PlayCount = stream.PlayCount,
            DisplayOrder = stream.DisplayOrder,
            CreatedAt = Timestamp.FromDateTime(stream.CreatedAt.ToUniversalTime())
        };
    }

    public override async Task<RadioStreamMessage> UpdateRadioStream(UpdateRadioStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("UpdateRadioStream request for stream ID {StreamId}", request.StreamId);

        await radioStreamService.UpdateStreamAsync(
            request.StreamId,
            request.Name,
            request.Url,
            string.IsNullOrWhiteSpace(request.FaviconUrl) ? null : request.FaviconUrl,
            string.IsNullOrWhiteSpace(request.FaviconFileName) ? null : request.FaviconFileName
        );

        var stream = await radioStreamService.GetStreamByIdAsync(request.StreamId);

        return new RadioStreamMessage
        {
            Id = stream!.Id,
            Name = stream.Name,
            Url = stream.Url,
            FaviconFileName = stream.FaviconFileName ?? string.Empty,
            PlayCount = stream.PlayCount,
            DisplayOrder = stream.DisplayOrder,
            CreatedAt = Timestamp.FromDateTime(stream.CreatedAt.ToUniversalTime()),
            LastPlayedAt = stream.LastPlayedAt.HasValue
                ? Timestamp.FromDateTime(stream.LastPlayedAt.Value.ToUniversalTime())
                : null
        };
    }

    public override async Task<DeleteRadioStreamReply> DeleteRadioStream(DeleteRadioStreamRequest request, ServerCallContext context)
    {
        logger.LogInformation("DeleteRadioStream request for stream ID {StreamId}", request.StreamId);

        await radioStreamService.DeleteStreamAsync(request.StreamId);
        return new DeleteRadioStreamReply { Success = true };
    }

    public override Task<SetRepeatModeReply> SetRepeatMode(SetRepeatModeRequest request, ServerCallContext context)
    {
        logger.LogInformation("SetRepeatMode request: {RepeatMode}", request.RepeatMode);
        musicPlayer.RepeatMode = request.RepeatMode;
        return Task.FromResult(new SetRepeatModeReply { Success = true });
    }

    public override Task<GetRepeatModeReply> GetRepeatMode(GetRepeatModeRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetRepeatModeReply { RepeatMode = musicPlayer.RepeatMode });
    }

    public override Task<SetSleepTimerReply> SetSleepTimer(SetSleepTimerRequest request, ServerCallContext context)
    {
        logger.LogInformation("SetSleepTimer request: {Minutes} minutes", request.Minutes);
        musicPlayer.SetSleepTimer(request.Minutes);
        return Task.FromResult(new SetSleepTimerReply { Success = true });
    }

    public override Task<CancelSleepTimerReply> CancelSleepTimer(CancelSleepTimerRequest request, ServerCallContext context)
    {
        logger.LogInformation("CancelSleepTimer request");
        musicPlayer.CancelSleepTimer();
        return Task.FromResult(new CancelSleepTimerReply { Success = true });
    }

    public override Task<GetSleepTimerReply> GetSleepTimer(GetSleepTimerRequest request, ServerCallContext context)
    {
        var remaining = musicPlayer.SleepTimerRemaining;
        return Task.FromResult(new GetSleepTimerReply
        {
            Active = musicPlayer.SleepTimerActive,
            RemainingSeconds = remaining.HasValue ? (int)remaining.Value.TotalSeconds : 0
        });
    }
}