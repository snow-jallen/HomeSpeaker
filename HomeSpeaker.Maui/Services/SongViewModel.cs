using HomeSpeaker.Shared;

namespace HomeSpeaker.Maui.Services;

public class SongViewModel
{
    public int SongId { get; set; }
    public required string Name { get; init; }
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
    public required string Album { get; init; }
    public required string Artist { get; init; }
    public string? Folder { get; private set; }
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
    public static SongViewModel ToSongViewModel(this SongMessage song)
    {
        return new SongViewModel
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