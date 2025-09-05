namespace HomeSpeaker.Server2;

public interface IFileSource
{
    IEnumerable<string> GetAllMp3s();
    void SoftDelete(string path);

    string RootFolder { get; }
}

public class DefaultFileSource : IFileSource
{
    private readonly string _rootFolder;
    private readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public DefaultFileSource(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    public string RootFolder => _rootFolder;

    public IEnumerable<string> GetAllMp3s()
    {
        var musicFolder = _rootFolder.Replace("~", _userProfile);

        if (!Directory.Exists(musicFolder))
        {
            Directory.CreateDirectory(musicFolder);
        }

        return Directory.GetFiles(musicFolder, "*.mp3", SearchOption.AllDirectories);
    }

    public void SoftDelete(string path)
    {
        var destFolder = Path.Combine(_userProfile, "DeletedMusic");
        if (!Directory.Exists(destFolder))
        {
            Directory.CreateDirectory(destFolder);
        }
        File.Move(path, Path.Combine(destFolder, Path.GetFileName(path)));
    }
}
