using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>Photos dropped into a folder (JPEG only, recursive). Never creates the folder itself.</summary>
public sealed class FolderPhotoSource(string folder) : IPhotoSource
{
    private ConcurrentDictionary<string, string> paths = new(StringComparer.Ordinal);

    public bool IsConfigured => Directory.Exists(folder);

    public Task<IReadOnlyList<ImmichAsset>> ListAssetsAsync(CancellationToken cancellationToken)
    {
        var map = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var assets = new List<ImmichAsset>();
        if (Directory.Exists(folder))
        {
            var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                .Select(path => (path, relative: Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')))
                .Where(f => isPhoto(f.relative))
                .OrderBy(f => f.relative, StringComparer.Ordinal);
            foreach (var (path, relative) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = idFor(relative);
                if (!map.TryAdd(id, path))
                {
                    continue;
                }

                assets.Add(new ImmichAsset(id, File.GetLastWriteTimeUtc(path), null, null, null));
            }
        }

        paths = map;
        return Task.FromResult<IReadOnlyList<ImmichAsset>>(assets);
    }

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        if (!paths.TryGetValue(assetId, out var path))
        {
            throw new FileNotFoundException($"Unknown photo id {assetId}");
        }

        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImmichFace>>([]);

    private static bool isPhoto(string relative)
    {
        var segments = relative.Split('/');
        if (segments.Any(s => s.StartsWith('.')))
        {
            return false;
        }

        var extension = Path.GetExtension(segments[^1]);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static string idFor(string relative)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(relative.ToLowerInvariant()));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
