using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Server2.Services;

public class HomeSpeakerService : HomeSpeakerBase
{
    private readonly ILogger<HomeSpeakerService> _logger;
    private readonly Mp3Library _library;
    private readonly IMusicPlayer _musicPlayer;
    private readonly YoutubeService _youtubeService;
    private readonly PlaylistService _playlistService;
    private readonly AmazonMusicService _amazonMusicService;
    private readonly List<IServerStreamWriter<StreamServerEvent>> _eventClients = new();
    private readonly List<IServerStreamWriter<StreamServerEvent>> _failedEvents = new();

    public HomeSpeakerService(ILogger<HomeSpeakerService> logger, Mp3Library library, IMusicPlayer musicPlayer, YoutubeService youtubeService, PlaylistService playlistService, AmazonMusicService amazonMusicService)
    {
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        _library = library ?? throw new System.ArgumentNullException(nameof(library));
        _musicPlayer = musicPlayer ?? throw new System.ArgumentNullException(nameof(musicPlayer));
        _youtubeService = youtubeService;
        _playlistService = playlistService;
        _amazonMusicService = amazonMusicService;
        _musicPlayer.PlayerEvent += MusicPlayer_PlayerEvent;
    }

    private async void MusicPlayer_PlayerEvent(object? sender, string message)
    {
        foreach (var client in _eventClients)
        {
            try
            {
                await client.WriteAsync(new StreamServerEvent { Message = message });
            }
            catch
            {
                _failedEvents.Add(client);
            }
        }

        if (_failedEvents.Any())
        {
            foreach (var client in _failedEvents)
            {
                _eventClients.Remove(client);
            }
            _failedEvents.Clear();
        }
    }

    public override Task<UpdateQueueReply> UpdateQueue(UpdateQueueRequest request, ServerCallContext context)
    {
        _musicPlayer.UpdateQueue(request.Songs);
        return Task.FromResult(new UpdateQueueReply());
    }

    public override async Task<PlayPlaylistReply> PlayPlaylist(PlayPlaylistRequest request, ServerCallContext context)
    {
        await _playlistService.PlayPlaylistAsync(request.PlaylistName);
        return new PlayPlaylistReply();
    }

    public override async Task<RenamePlaylistReply> RenamePlaylist(RenamePlaylistRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received RenamePlaylist request: {oldName} -> {newName}", request.OldName, request.NewName);
        await _playlistService.RenamePlaylistAsync(request.OldName, request.NewName);
        _logger.LogInformation("Successfully renamed playlist: {oldName} -> {newName}", request.OldName, request.NewName);
        return new RenamePlaylistReply();
    }

    public override async Task<DeletePlaylistReply> DeletePlaylist(DeletePlaylistRequest request, ServerCallContext context)
    {
        await _playlistService.DeletePlaylistAsync(request.PlaylistName);
        return new DeletePlaylistReply();
    }

    public override async Task<ReorderPlaylistSongsReply> ReorderPlaylistSongs(ReorderPlaylistSongsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received ReorderPlaylistSongs request for playlist: {playlistName}", request.PlaylistName);
        await _playlistService.ReorderPlaylistSongsAsync(request.PlaylistName, request.SongPaths.ToList());
        _logger.LogInformation("Successfully reordered songs in playlist: {playlistName}", request.PlaylistName);
        return new ReorderPlaylistSongsReply();
    }

    public override async Task<AddSongToPlaylistReply> AddSongToPlaylist(AddSongToPlaylistRequest request, ServerCallContext context)
    {
        await _playlistService.AppendSongToPlaylistAsync(request.PlaylistName, request.SongPath);
        return new AddSongToPlaylistReply();
    }

    public override async Task<RemoveSongFromPlaylistReply> RemoveSongFromPlaylist(RemoveSongFromPlaylistRequest request, ServerCallContext context)
    {
        await _playlistService.RemoveSongFromPlaylistAsync(request.PlaylistName, request.SongPath);
        return new RemoveSongFromPlaylistReply();
    }

    public override async Task<GetPlaylistsReply> GetPlaylists(GetPlaylistsRequest request, ServerCallContext context)
    {
        var reply = new GetPlaylistsReply();
        var playlists = await _playlistService.GetPlaylistsAsync();
        foreach (var playlist in playlists)
        {
            var playlistMessage = new PlaylistMessage
            {
                PlaylistName = playlist.Name
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
        _library.DeleteSong(request.SongId);
        return Task.FromResult(new DeleteSongReply());
    }

    public override Task<UpdateSongReply> UpdateSong(UpdateSongRequest request, ServerCallContext context)
    {
        _library.UpdateSong(request.SongId, request.Name, request.Artist, request.Album);
        return Task.FromResult(new UpdateSongReply());
    }

    public override async Task<SearchVideoReply> SearchViedo(SearchVideoRequest request, ServerCallContext context)
    {
        var videos = await _youtubeService.SearchAsync(request.SearchTerm);
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
        var streamingProgress = new StreamingProgress(responseStream, v.Title, _logger);
        await _youtubeService.CacheVideoAsync(v.Id, v.Title, streamingProgress);
        _library.IsDirty = true;
    }

    public override async Task GetSongs(GetSongsRequest request, IServerStreamWriter<GetSongsReply> responseStream, ServerCallContext context)
    {
        var reply = new GetSongsReply();
        if (_library?.Songs?.Any() ?? false)
        {
            IEnumerable<Song> songs = _library.Songs;
            if (!string.IsNullOrEmpty(request.Folder))
            {
                _logger.LogInformation("Filtering songs to just those in the {folder} folder", request.Folder);
                songs = songs.Where(s => s.Path.Contains(request.Folder));
            }
            _logger.LogInformation("Found songs!  Sending to client.");
            var songMessages = translateSongs(songs);
            reply.Songs.AddRange(songMessages);
        }
        else
        {
            _logger.LogInformation("No songs found.  Sending back empty list.");
        }
        await responseStream.WriteAsync(reply);
    }

    public override Task<PlaySongReply> PlaySong(PlaySongRequest request, ServerCallContext context)
    {
        _logger.LogInformation("PlaySong request for {songid}", request.SongId);

        var song = _library.Songs.FirstOrDefault(s => s.SongId == request.SongId);

        var reply = new PlaySongReply { Ok = false };
        if (song != null)
        {
            _ = Task.Run(() =>
                _musicPlayer.PlaySong(song)
            );
            reply.Ok = true;
        }
        else
        {
            _logger.LogWarning("Song {songid} not found in _library.", request.SongId);
        }
        return Task.FromResult(reply);
    }

    public override Task<PlaySongReply> PlayStream(PlayStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("PlayStream request for {streamurl}", request.StreamUrl);
        _musicPlayer.PlayStream(request.StreamUrl);
        return Task.FromResult(new PlaySongReply { Ok = true });
    }

    public override Task<PlaySongReply> EnqueueSong(PlaySongRequest request, ServerCallContext context)
    {
        _logger.LogInformation("EnqueueSong request for {songid}", request.SongId);

        var song = _library.Songs.FirstOrDefault(s => s.SongId == request.SongId);
        var reply = new PlaySongReply { Ok = false };
        if (song != null)
        {
            _logger.LogInformation($"Queuing up #{song.SongId}: {song.Name}");
            _musicPlayer.EnqueueSong(song);
            reply.Ok = true;
        }
        else
        {
            _logger.LogWarning("Song {songid} not found in library", request.SongId);
        }

        return Task.FromResult(reply);
    }

    public override async Task<GetStatusReply> GetPlayerStatus(GetStatusRequest request, ServerCallContext context)
    {
        var status = _musicPlayer.Status ?? new Shared.PlayerStatus();
        var currentVolume = await _musicPlayer.GetVolume();
        return new GetStatusReply
        {
            Elapsed = Duration.FromTimeSpan(status.Elapsed),
            PercentComplete = (double)status.PercentComplete,
            Remaining = Duration.FromTimeSpan(status.Remaining),
            StilPlaying = status.StillPlaying,
            CurrentSong = status.CurrentSong != null ? translateSong(status.CurrentSong) : null,
            Volume = currentVolume
        };
    }

    public override async Task GetPlayQueue(GetSongsRequest request, IServerStreamWriter<GetSongsReply> responseStream, ServerCallContext context)
    {
        var reply = new GetSongsReply();
        System.Collections.Generic.IEnumerable<Shared.Song> songQueue = _musicPlayer.SongQueue;
        if (songQueue.Any())
        {
            _logger.LogInformation("Found songs in queue!  Sending to client.");
            reply.Songs.AddRange(translateSongs(songQueue));
        }
        else
        {
            _logger.LogInformation("No songs in queue.  Sending back empty list.");
        }
        await responseStream.WriteAsync(reply);
    }

    private IEnumerable<SongMessage> translateSongs(IEnumerable<Song> songQueue)
    {
        return songQueue.Select(translateSong);
    }

    private SongMessage translateSong(Song s)
    {
        //string? path = s?.Path.Replace(_library.RootFolder, string.Empty, StringComparison.InvariantCultureIgnoreCase).Substring(1);
        //if (path == s?.Path)
        //{
        //    _logger.LogWarning("what? orig {orig} is same as {new}", s.Path, path);
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
            _musicPlayer.ClearQueue();
        }
        if (request.Play)
        {
            _musicPlayer.ResumePlay();
        }
        if (request.SkipToNext)
        {
            _musicPlayer.SkipToNext();
        }
        if (request.Stop)
        {
            _musicPlayer.Stop();
        }
        if (request.SetVolume)
        {
            _musicPlayer.SetVolume(request.VolumeLevel);
        }
        return Task.FromResult(new PlayerControlReply());
    }

    public override Task<ShuffleQueueReply> ShuffleQueue(ShuffleQueueRequest request, ServerCallContext context)
    {
        _musicPlayer.ShuffleQueue();
        return Task.FromResult(new ShuffleQueueReply());
    }

    public override Task<EnqueueFolderReply> EnqueueFolder(EnqueueFolderRequest request, ServerCallContext context)
    {
        foreach (var song in _library.Songs.Where(s => s.Path.Contains(request.FolderPath)))
        {
            _musicPlayer.EnqueueSong(song);
        }
        return Task.FromResult(new EnqueueFolderReply());
    }

    public override Task<PlayFolderReply> PlayFolder(PlayFolderRequest request, ServerCallContext context)
    {
        _musicPlayer.Stop();
        foreach (var song in _library.Songs.Where(s => s.Path.Contains(request.FolderPath)))
        {
            _musicPlayer.EnqueueSong(song);
        }
        return Task.FromResult(new PlayFolderReply());
    }

    public override async Task SendEvent(Empty request, IServerStreamWriter<StreamServerEvent> responseStream, ServerCallContext context)
    {
        _eventClients.Add(responseStream);
        await responseStream.WriteAsync(new StreamServerEvent { Message = "Client connected." });
        await Task.Delay(TimeSpan.FromMinutes(180));
    }

    public override async Task<Empty> ToggleBacklight(Empty request, ServerCallContext context)
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback =
            (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            };

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://192.168.1.111:5001") };
        var currentBrightness = int.Parse(await client.GetStringAsync("/get"));
        var newBrightness = currentBrightness switch
        {
            > 200 => 20,
            _ => 255
        };
        _logger.LogInformation("Trying to set brightness to {brightness}", newBrightness);
        var response = await client.GetAsync($"/set?brightness={newBrightness}");
        _logger.LogInformation("response: {response}", response);

        return new Empty();
    }

    public override async Task<GetAmazonPlaylistsReply> GetAmazonPlaylists(GetAmazonPlaylistsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetAmazonPlaylists called");
        
        var playlists = await _amazonMusicService.GetAmazonPlaylistsAsync();
        var reply = new GetAmazonPlaylistsReply();
        
        reply.Playlists.AddRange(playlists.Select(p => new AmazonPlaylistMessage
        {
            PlaylistId = p.PlaylistId,
            PlaylistName = p.PlaylistName,
            TrackCount = p.TrackCount
        }));
        
        _logger.LogInformation("Returning {Count} Amazon playlists", reply.Playlists.Count);
        return reply;
    }

    public override async Task<PlayAmazonPlaylistReply> PlayAmazonPlaylist(PlayAmazonPlaylistRequest request, ServerCallContext context)
    {
        _logger.LogInformation("PlayAmazonPlaylist called with PlaylistId: {PlaylistId}", request.PlaylistId);
        
        await _amazonMusicService.PlayAmazonPlaylistAsync(request.PlaylistId);
        
        return new PlayAmazonPlaylistReply();
    }
}
