using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Diagnostics;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.WebAssembly.Services;

public class HomeSpeakerService
{
    private HomeSpeakerClient client;
    private List<SongMessage> songs = new();
    private readonly ILogger<HomeSpeakerService> logger;
    public IEnumerable<SongMessage> Songs => songs;
    public event EventHandler? QueueChanged;

    public HomeSpeakerService(IConfiguration config, ILogger<HomeSpeakerService> logger, IWebAssemblyHostEnvironment hostEnvironment)
    {
        string address = config["ServerAddress"] ?? throw new MissingConfigException("ServerAddress");
        logger.LogInformation($"I was about to use {address}");
        address = hostEnvironment.BaseAddress;
        logger.LogInformation("But instead I'll use {address}", address);
        var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });

        client = new HomeSpeakerClient(channel);
        this.logger = logger;
        _ = listenForEvents();
    }

    public HomeSpeakerClient HomeSpeakerClient => client;
    
    private async Task listenForEvents()
    {
        try
        {
            var eventReply = client.SendEvent(new Google.Protobuf.WellKnownTypes.Empty());
            await foreach (var eventInstance in eventReply.ResponseStream.ReadAllAsync())
            {
                StatusChanged?.Invoke(this, eventInstance.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listening for events from server");
            // Could implement reconnection logic here
        }
    }

    public async Task ToggleBrightness()
    {
        await client.ToggleBacklightAsync(new Google.Protobuf.WellKnownTypes.Empty());
    }

    public async Task SetVolumeAsync(int volume0to100)
    {
        var request = new PlayerControlRequest { SetVolume = true, VolumeLevel = volume0to100 };
        await client.PlayerControlAsync(request);
    }

    public async Task<int> GetVolumeAsync()
    {
        var status = await GetStatusAsync();
        return status.Volume;
    }

    public async Task UpdateQueueAsync(List<SongViewModel> songs)
    {
        var request = new UpdateQueueRequest();
        request.Songs.AddRange(songs.Select(s => s.Path));
        await client.UpdateQueueAsync(request);
    }

    public async Task PlayStreamAsync(string streamUri) => await client.PlayStreamAsync(new PlayStreamRequest { StreamUrl = streamUri });

    public async Task<GetStatusReply> GetStatusAsync()
    {
        return await client.GetPlayerStatusAsync(new GetStatusRequest());
    }

    public async Task EnqueueFolderAsync(SongGroup songs) => await client.EnqueueFolderAsync(new EnqueueFolderRequest { FolderPath = songs.FolderPath });


    public async Task<IEnumerable<SongViewModel>> GetSongsInFolder(string folder)
    {
        var songs = new List<SongViewModel>();
        var getSongsReply = client.GetSongs(new GetSongsRequest { Folder = folder });
        await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
        {
            songs.AddRange(reply.Songs.Select(s => s.ToSongViewModel()));
        }

        return songs;
    }

    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync() => (await client.GetPlaylistsAsync(new GetPlaylistsRequest()))
            .Playlists
            .Select(p => new Playlist(
                p.PlaylistName,
                p.Songs.Select(s => s.ToSong())
            ));

    public async Task AddToPlaylistAsync(string playlistName, string songPath)
    {
        await client.AddSongToPlaylistAsync(new AddSongToPlaylistRequest { PlaylistName = playlistName, SongPath = songPath });
    }

    public async Task RemoveFromPlaylistAsync(string playlistName, string songPath)
    {
        await client.RemoveSongFromPlaylistAsync(new RemoveSongFromPlaylistRequest { PlaylistName = playlistName, SongPath = songPath });
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        await client.PlayPlaylistAsync(new PlayPlaylistRequest { PlaylistName = playlistName });
    }    public async Task RenamePlaylistAsync(string oldName, string newName)
    {
        try
        {
            logger.LogInformation("Calling RenamePlaylist gRPC method: {oldName} -> {newName}", oldName, newName);
            await client.RenamePlaylistAsync(new RenamePlaylistRequest { OldName = oldName, NewName = newName });
            logger.LogInformation("Successfully called RenamePlaylist gRPC method: {oldName} -> {newName}", oldName, newName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling RenamePlaylist gRPC method: {oldName} -> {newName}", oldName, newName);
            throw;
        }
    }

    public async Task DeletePlaylistAsync(string playlistName)
    {
        await client.DeletePlaylistAsync(new DeletePlaylistRequest { PlaylistName = playlistName });
    }

    public async Task ReorderPlaylistSongsAsync(string playlistName, List<string> songPaths)
    {
        logger.LogInformation("Calling ReorderPlaylistSongs gRPC method for playlist: {playlistName}", playlistName);
        await client.ReorderPlaylistSongsAsync(new ReorderPlaylistSongsRequest 
        { 
            PlaylistName = playlistName, 
            SongPaths = { songPaths } 
        });
        logger.LogInformation("Successfully called ReorderPlaylistSongs gRPC method for playlist: {playlistName}", playlistName);
    }

    readonly char[] separators = new[] { '/', '\\' };

    public async Task<IEnumerable<string>> GetFolders()
    {
        List<string> folders = new();

        logger.LogInformation("User wanted folders, so first I'll get all the songs.");

        var getSongsReply = client.GetSongs(new GetSongsRequest { });
        await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
        {
            foreach (var s in reply.Songs)
            {
                var parts = s.Path.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                //var directory = s.Path.Replace(parts.Last(), string.Empty);
                var directory = parts[0];

                if (directory == "c:")
                {
                    logger.LogInformation("Directory: '{directory}' ({path})", directory, s.Path);
                }

                if (!folders.Contains(directory))
                {
                    logger.LogInformation("Directory: '{directory}' ({path})", directory, s.Path);
                    folders.Add(directory);
                }
            }
        }

        return folders;
    }

    public async Task<IEnumerable<SongViewModel>> GetAllSongsAsync()
    {
        using var source = new ActivitySource("BlazorUI");
        using (var activity = source.StartActivity("GetAllSongs", ActivityKind.Client))
        {
            activity?.AddEvent(new ActivityEvent("Calling GetAllSongs()"));
            activity?.SetTag("tag1", "value1");
            logger.LogWarning("Trying to send otel trace!");

            var songs = new List<SongViewModel>();
            var getSongsReply = client.GetSongs(new GetSongsRequest { });
            await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
            {
                songs.AddRange(reply.Songs.Select(s => s.ToSongViewModel()));
            }

            return songs;
        }
    }

    public async Task<Dictionary<string, List<SongViewModel>>> GetSongGroups()
    {
        var groups = new Dictionary<string, List<SongViewModel>>();
        var getSongsReply = client.GetSongs(new GetSongsRequest { });
        //var starredSongs = (await database.GetStarredSongsAsync()).Select(s => s.Path).ToList();
        await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
        {
            foreach (var s in reply.Songs/*.Where(s => starredSongs.Contains(s.Path) == false)*/)
            {
                var song = s.ToSongViewModel();
                if (song.Folder == null)
                {
                    continue;
                }

                if (groups.ContainsKey(song.Folder) is false)
                {
                    groups[song.Folder] = new List<SongViewModel>();
                }

                groups[song.Folder].Add(song);
            }
        }

        return groups;
    }

    public async Task PlaySongAsync(int songId)
    {
        await client.PlayerControlAsync(new PlayerControlRequest { Stop = true, ClearQueue = true });
        await client.EnqueueSongAsync(new PlaySongRequest { SongId = songId });
    }

    public async Task<IEnumerable<SongViewModel>> GetPlayQueueAsync()
    {
        var queue = new List<SongViewModel>();
        var queueResponse = client.GetPlayQueue(new GetSongsRequest());
        await foreach (var reply in queueResponse.ResponseStream.ReadAllAsync())
        {
            queue.AddRange(reply.Songs.Select(s => s.ToSongViewModel()));
        }
        return queue;
    }

    public async Task EnqueueSongAsync(int songId) => await client.EnqueueSongAsync(new PlaySongRequest { SongId = songId });
    public async Task PlayFolderAsync(string folder) => await client.PlayFolderAsync(new PlayFolderRequest { FolderPath = folder });
    public async Task EnqueueFolderAsync(string folder) => await client.EnqueueFolderAsync(new EnqueueFolderRequest { FolderPath = folder });
    public async Task StopPlayingAsync() => await client.PlayerControlAsync(new PlayerControlRequest { Stop = true });
    public async Task ClearQueueAsync()
    {
        await client.PlayerControlAsync(new PlayerControlRequest { ClearQueue = true });
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SkipToNextAsync()
    {
        await client.PlayerControlAsync(new PlayerControlRequest { SkipToNext = true });
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResumePlayAsync()
    {
        await client.PlayerControlAsync(new PlayerControlRequest { Play = true });
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ShuffleQueueAsync()
    {
        await client.ShuffleQueueAsync(new ShuffleQueueRequest());
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<string>? StatusChanged;
}
