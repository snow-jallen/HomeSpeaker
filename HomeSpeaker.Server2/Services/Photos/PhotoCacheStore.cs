using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Owns the on-disk photo cache: preview JPEGs named {assetId}.jpg, manifest.json and hidden.json.
/// Every write goes to a .tmp file first and is then renamed, so readers never see a partial file.
/// </summary>
public sealed class PhotoCacheStore
{
    private const string ManifestFile = "manifest.json";
    private const string HiddenFile = "hidden.json";
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly Lock hiddenGate = new();
    private readonly ILogger<PhotoCacheStore> logger;

    public PhotoCacheStore(string rootPath, ILogger<PhotoCacheStore> logger)
    {
        RootPath = Path.GetFullPath(rootPath);
        this.logger = logger;
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public static bool IsValidAssetId(string assetId) => Guid.TryParse(assetId, out _);

    public IReadOnlyList<PhotoEntry> LoadManifest() => readJson<List<PhotoEntry>>(ManifestFile) ?? [];

    public void SaveManifest(IReadOnlyList<PhotoEntry> entries) => writeJsonAtomic(ManifestFile, entries);

    public bool HasImage(string assetId) => IsValidAssetId(assetId) && File.Exists(imagePath(assetId));

    public bool TryGetImagePath(string assetId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (!HasImage(assetId))
        {
            return false;
        }

        path = imagePath(assetId);
        return true;
    }

    public async Task SaveImageAsync(string assetId, Func<Stream, Task> writeContent)
    {
        if (!IsValidAssetId(assetId))
        {
            throw new ArgumentException($"Not a valid asset id: {assetId}", nameof(assetId));
        }

        var finalPath = imagePath(assetId);
        var tempPath = finalPath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await writeContent(stream);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            tryDelete(tempPath);
            throw;
        }
    }

    public void DeleteImage(string assetId)
    {
        if (IsValidAssetId(assetId))
        {
            tryDelete(imagePath(assetId));
        }
    }

    public IReadOnlySet<string> LoadHidden()
    {
        lock (hiddenGate)
        {
            return readJson<HashSet<string>>(HiddenFile) ?? [];
        }
    }

    public void AddHidden(string assetId)
    {
        lock (hiddenGate)
        {
            var hidden = readJson<HashSet<string>>(HiddenFile) ?? [];
            if (hidden.Add(assetId))
            {
                writeJsonAtomic(HiddenFile, hidden);
            }
        }
    }

    private string imagePath(string assetId) => Path.Combine(RootPath, assetId + ".jpg");

    private T? readJson<T>(string fileName)
        where T : class
    {
        var path = Path.Combine(RootPath, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Could not read {File}; treating it as empty", path);
            return null;
        }
    }

    private void writeJsonAtomic<T>(string fileName, T value)
    {
        var path = Path.Combine(RootPath, fileName);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, json));
        File.Move(tempPath, path, overwrite: true);
    }

    private void tryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete {File}", path);
        }
    }
}
