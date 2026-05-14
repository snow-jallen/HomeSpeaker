using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public sealed class OfflineDownloadService
{
    private readonly MusicContext dbContext;
    private readonly Mp3Library library;
    private readonly ILogger<OfflineDownloadService> logger;

    public OfflineDownloadService(
        MusicContext dbContext,
        Mp3Library library,
        ILogger<OfflineDownloadService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.library = library ?? throw new ArgumentNullException(nameof(library));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OfflineDownloadManifestDto> GetManifestAsync(CancellationToken cancellationToken)
    {
        var targets = await dbContext.OfflineDownloadTargets
            .OrderByDescending(target => target.CreatedUtc)
            .ThenByDescending(target => target.Id)
            .ToListAsync(cancellationToken);

        var librarySongs = getAvailableSongs();
        var targetResolutions = targets
            .Select(target => resolveTarget(target, librarySongs))
            .ToList();

        var downloadSongs = buildDownloadSongs(targetResolutions);

        return new OfflineDownloadManifestDto
        {
            GeneratedUtc = DateTime.UtcNow,
            Targets = targetResolutions.Select(createTargetDto).ToList(),
            Songs = downloadSongs
        };
    }

    public async Task<OfflineDownloadTargetDto> AddTargetAsync(OfflineDownloadTargetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var librarySongs = getAvailableSongs();
        var normalizedTarget = normalizeRequest(request, librarySongs);

        var existingTarget = await dbContext.OfflineDownloadTargets
            .FirstOrDefaultAsync(
                target => target.TargetType == normalizedTarget.TargetType
                    && target.ArtistName == normalizedTarget.ArtistName
                    && target.AlbumName == normalizedTarget.AlbumName
                    && target.SongPath == normalizedTarget.SongPath,
                cancellationToken);

        if (existingTarget is null)
        {
            existingTarget = normalizedTarget;
            dbContext.OfflineDownloadTargets.Add(existingTarget);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Created offline download target {TargetType}::{DisplayName}", existingTarget.TargetType, getDisplayName(existingTarget, null));
        }
        else
        {
            logger.LogInformation("Offline download target already exists for {TargetType}::{DisplayName}", existingTarget.TargetType, getDisplayName(existingTarget, null));
        }

        var resolution = resolveTarget(existingTarget, librarySongs);
        return createTargetDto(resolution);
    }

    public async Task<bool> RemoveTargetAsync(int targetId, CancellationToken cancellationToken)
    {
        var target = await dbContext.OfflineDownloadTargets.FindAsync([targetId], cancellationToken);
        if (target is null)
        {
            return false;
        }

        dbContext.OfflineDownloadTargets.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Removed offline download target {TargetId}", targetId);
        return true;
    }

    public OfflineDownloadMediaResult? GetMedia(string songPath)
    {
        if (string.IsNullOrWhiteSpace(songPath))
        {
            return null;
        }

        var song = getAvailableSongs().FirstOrDefault(candidate =>
            string.Equals(candidate.Path, songPath, StringComparison.OrdinalIgnoreCase));

        if (song?.Path is null)
        {
            return null;
        }

        var resolvedPath = Path.GetFullPath(song.Path);
        var fileInfo = new FileInfo(resolvedPath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        return new OfflineDownloadMediaResult
        {
            FilePath = resolvedPath,
            ContentType = "audio/mpeg",
            DownloadFileName = fileInfo.Name,
            ETag = createEtag(fileInfo),
            LastModifiedUtc = fileInfo.LastWriteTimeUtc
        };
    }

    private List<Song> getAvailableSongs() =>
        library.Songs
            .Where(song => !string.IsNullOrWhiteSpace(song.Path) && File.Exists(song.Path))
            .OrderBy(song => song.Artist)
            .ThenBy(song => song.Album)
            .ThenBy(song => song.Name)
            .ToList();

    private OfflineDownloadTarget normalizeRequest(OfflineDownloadTargetRequest request, IReadOnlyCollection<Song> librarySongs)
    {
        return request.TargetType switch
        {
            OfflineDownloadTargetType.Artist => createArtistTarget(request, librarySongs),
            OfflineDownloadTargetType.Album => createAlbumTarget(request, librarySongs),
            OfflineDownloadTargetType.Song => createSongTarget(request, librarySongs),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.TargetType, "Unsupported offline target type.")
        };
    }

    private static OfflineDownloadTarget createArtistTarget(OfflineDownloadTargetRequest request, IReadOnlyCollection<Song> librarySongs)
    {
        var artistName = request.ArtistName?.Trim();
        if (string.IsNullOrWhiteSpace(artistName))
        {
            throw new ArgumentException("ArtistName is required when marking an artist for offline use.", nameof(request));
        }

        var matchedSongs = librarySongs
            .Where(song => string.Equals(song.Artist, artistName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchedSongs.Count == 0)
        {
            throw new KeyNotFoundException($"Artist '{artistName}' was not found in the library.");
        }

        return new OfflineDownloadTarget
        {
            TargetType = OfflineDownloadTargetType.Artist,
            ArtistName = matchedSongs[0].Artist ?? artistName,
            AlbumName = string.Empty,
            SongPath = string.Empty,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static OfflineDownloadTarget createAlbumTarget(OfflineDownloadTargetRequest request, IReadOnlyCollection<Song> librarySongs)
    {
        var albumName = request.AlbumName?.Trim();
        var artistName = request.ArtistName?.Trim();

        if (string.IsNullOrWhiteSpace(albumName))
        {
            throw new ArgumentException("AlbumName is required when marking an album for offline use.", nameof(request));
        }

        var matchedSongs = librarySongs
            .Where(song =>
                string.Equals(song.Album, albumName, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(artistName) || string.Equals(song.Artist, artistName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matchedSongs.Count == 0)
        {
            throw new KeyNotFoundException(
                string.IsNullOrWhiteSpace(artistName)
                    ? $"Album '{albumName}' was not found in the library."
                    : $"Album '{albumName}' by '{artistName}' was not found in the library.");
        }

        return new OfflineDownloadTarget
        {
            TargetType = OfflineDownloadTargetType.Album,
            ArtistName = matchedSongs[0].Artist ?? artistName ?? string.Empty,
            AlbumName = matchedSongs[0].Album ?? albumName,
            SongPath = string.Empty,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static OfflineDownloadTarget createSongTarget(OfflineDownloadTargetRequest request, IReadOnlyCollection<Song> librarySongs)
    {
        Song? matchedSong = null;

        if (request.SongId.HasValue)
        {
            matchedSong = librarySongs.FirstOrDefault(song => song.SongId == request.SongId.Value);
        }

        if (matchedSong is null && !string.IsNullOrWhiteSpace(request.SongPath))
        {
            matchedSong = librarySongs.FirstOrDefault(song =>
                string.Equals(song.Path, request.SongPath.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (matchedSong?.Path is null)
        {
            throw new KeyNotFoundException("Song was not found in the library.");
        }

        return new OfflineDownloadTarget
        {
            TargetType = OfflineDownloadTargetType.Song,
            ArtistName = matchedSong.Artist ?? string.Empty,
            AlbumName = matchedSong.Album ?? string.Empty,
            SongPath = matchedSong.Path,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private OfflineDownloadTargetResolution resolveTarget(OfflineDownloadTarget target, IReadOnlyCollection<Song> librarySongs)
    {
        var matchedSongs = target.TargetType switch
        {
            OfflineDownloadTargetType.Artist => librarySongs
                .Where(song => string.Equals(song.Artist, target.ArtistName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(song => song.Album)
                .ThenBy(song => song.Name)
                .ToList(),
            OfflineDownloadTargetType.Album => librarySongs
                .Where(song =>
                    string.Equals(song.Album, target.AlbumName, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(target.ArtistName) || string.Equals(song.Artist, target.ArtistName, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(song => song.Name)
                .ToList(),
            OfflineDownloadTargetType.Song => librarySongs
                .Where(song => string.Equals(song.Path, target.SongPath, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            _ => []
        };

        return new OfflineDownloadTargetResolution(target, matchedSongs);
    }

    private static IReadOnlyList<OfflineDownloadSongDto> buildDownloadSongs(IEnumerable<OfflineDownloadTargetResolution> resolutions)
    {
        var songsByPath = new Dictionary<string, OfflineDownloadSongBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var resolution in resolutions)
        {
            foreach (var song in resolution.MatchedSongs)
            {
                if (song.Path is null)
                {
                    continue;
                }

                if (!songsByPath.TryGetValue(song.Path, out var builder))
                {
                    builder = new OfflineDownloadSongBuilder(song);
                    songsByPath[song.Path] = builder;
                }

                builder.Sources.Add(new OfflineDownloadSourceDto
                {
                    TargetId = resolution.Target.Id,
                    TargetType = resolution.Target.TargetType,
                    DisplayName = getDisplayName(resolution.Target, song)
                });
            }
        }

        return songsByPath.Values
            .Select(builder => builder.ToDto())
            .OrderBy(dto => dto.Song.Artist)
            .ThenBy(dto => dto.Song.Album)
            .ThenBy(dto => dto.Song.Name)
            .ToList();
    }

    private static OfflineDownloadTargetDto createTargetDto(OfflineDownloadTargetResolution resolution)
    {
        var matchedSong = resolution.Target.TargetType == OfflineDownloadTargetType.Song && resolution.MatchedSongs.Count > 0
            ? resolution.MatchedSongs[0]
            : null;

        return new OfflineDownloadTargetDto
        {
            Id = resolution.Target.Id,
            TargetType = resolution.Target.TargetType,
            Status = resolution.MatchedSongs.Count > 0 ? OfflineDownloadTargetStatus.Ready : OfflineDownloadTargetStatus.Missing,
            DisplayName = getDisplayName(resolution.Target, matchedSong),
            ArtistName = string.IsNullOrWhiteSpace(resolution.Target.ArtistName) ? null : resolution.Target.ArtistName,
            AlbumName = string.IsNullOrWhiteSpace(resolution.Target.AlbumName) ? null : resolution.Target.AlbumName,
            SongPath = string.IsNullOrWhiteSpace(resolution.Target.SongPath) ? null : resolution.Target.SongPath,
            Song = matchedSong,
            ResolvedSongCount = resolution.MatchedSongs.Count,
            CreatedUtc = resolution.Target.CreatedUtc
        };
    }

    private static string getDisplayName(OfflineDownloadTarget target, Song? matchedSong)
    {
        return target.TargetType switch
        {
            OfflineDownloadTargetType.Artist => target.ArtistName,
            OfflineDownloadTargetType.Album when !string.IsNullOrWhiteSpace(target.ArtistName) => $"{target.ArtistName} — {target.AlbumName}",
            OfflineDownloadTargetType.Album => target.AlbumName,
            OfflineDownloadTargetType.Song when matchedSong is not null && !string.IsNullOrWhiteSpace(matchedSong.Artist) => $"{matchedSong.Artist} — {matchedSong.Name}",
            OfflineDownloadTargetType.Song when matchedSong is not null => matchedSong.Name,
            OfflineDownloadTargetType.Song => Path.GetFileName(target.SongPath),
            _ => string.Empty
        };
    }

    private static string createEtag(FileInfo fileInfo) =>
        $"\"{fileInfo.Length:x}-{fileInfo.LastWriteTimeUtc.Ticks:x}\"";

    private sealed record OfflineDownloadTargetResolution(
        OfflineDownloadTarget Target,
        IReadOnlyList<Song> MatchedSongs);

    private sealed class OfflineDownloadSongBuilder
    {
        private readonly Song song;

        public OfflineDownloadSongBuilder(Song song)
        {
            this.song = song;
        }

        public List<OfflineDownloadSourceDto> Sources { get; } = [];

        public OfflineDownloadSongDto ToDto()
        {
            var fileInfo = new FileInfo(song.Path!);

            return new OfflineDownloadSongDto
            {
                Song = song,
                SongPath = song.Path!,
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                ETag = createEtag(fileInfo),
                DownloadUrl = $"/api/homespeaker/offline/media?songPath={Uri.EscapeDataString(song.Path!)}",
                Sources = Sources
                    .DistinctBy(source => source.TargetId)
                    .OrderBy(source => source.DisplayName)
                    .ToList()
            };
        }
    }
}

public sealed record OfflineDownloadMediaResult
{
    public string FilePath { get; init; } = string.Empty;
    public string ContentType { get; init; } = "audio/mpeg";
    public string DownloadFileName { get; init; } = string.Empty;
    public string ETag { get; init; } = string.Empty;
    public DateTime LastModifiedUtc { get; init; }
}
