namespace HomeSpeaker.Server2;

public interface IFileSource
{
    IEnumerable<string> GetAllMp3s();
    void SoftDelete(string path);

    string RootFolder { get; }
}

public class DefaultFileSource : IFileSource
{
    private readonly string rootFolder;
    private readonly string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public DefaultFileSource(string rootFolder)
    {
        this.rootFolder = rootFolder;
    }

    public string RootFolder => rootFolder;

    public IEnumerable<string> GetAllMp3s()
    {
        var musicFolder = rootFolder.Replace("~", userProfile);

        if (!Directory.Exists(musicFolder))
        {
            Directory.CreateDirectory(musicFolder);
        }

        return Directory.GetFiles(musicFolder, "*.mp3", SearchOption.AllDirectories);
    }

    public void SoftDelete(string path)
    {
        var destFolder = Path.Combine(userProfile, "DeletedMusic");
        if (!Directory.Exists(destFolder))
        {
            Directory.CreateDirectory(destFolder);
        }
        File.Move(path, Path.Combine(destFolder, Path.GetFileName(path)));
    }
}
