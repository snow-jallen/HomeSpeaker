//using Android.Database;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Maui.ViewModels;

public partial class SongViewModel(HomeSpeakerClientService client) : ObservableObject
{
    [ObservableProperty]
    public int _songId;

    [ObservableProperty]
    public string _name;

    private string? path;
    public string? Path
    {
        get => path;
        set
        {
            path = value;
            if (path?.Contains('\\') ?? false)
                Folder = System.IO.Path.GetDirectoryName(path.Replace('\\', '/'));
            else
                Folder = System.IO.Path.GetDirectoryName(path);
        }
    }

    [ObservableProperty]
    private string _album;

    [ObservableProperty]
    private string _artist;

    [ObservableProperty]
    private string? _folder;

    [RelayCommand]
    private async Task PlaySong()
    {
        await client.PlaySongAsync(SongId);
    }
}

public partial class SongGroup : List<SongViewModel>
{
    public string FolderName { get; set; }
    public string FolderPath { get; set; }

    public SongGroup(string name, List<SongViewModel> songs) : base(songs)
    {
        var parts = name.Split('/', '\\');
        FolderName = parts.Last();
        FolderPath = name;
    }
}

public static class ViewModelExtensions
{
    public static SongViewModel ToSongViewModel(this SongMessage song, HomeSpeakerClientService client)
    {
        return new SongViewModel(client)
        {
            SongId = song?.SongId ?? -1,
            Name = song?.Name?.Trim() ?? "[ Null Song Response ??? ]",
            Album = song?.Album?.Trim() ?? "[ No Album ]",
            Artist = song?.Artist?.Trim() ?? "[ No Artist ]",
            Path = song?.Path?.Trim()
        };
    }
    //public async static IAsyncEnumerable<T> ReadAllAsync<T>(this IAsyncStreamReader<T> streamReader, CancellationToken cancellationToken = default)
    //{
    //    if (streamReader == null)
    //    {
    //        throw new System.ArgumentNullException(nameof(streamReader));
    //    }

    //    while (await streamReader.MoveNext(cancellationToken))
    //    {
    //        yield return streamReader.Current;
    //    }
    //}
}