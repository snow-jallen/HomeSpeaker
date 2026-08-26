using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public sealed class AutoPlayService
{
    private const string DefaultPlaylistName = "hymns for home and church";

    private readonly MusicContext dbContext;
    private readonly IMusicPlayer musicPlayer;
    private readonly PlaylistService playlistService;
    private readonly RadioStreamService radioStreamService;
    private readonly PlayerStateService playerStateService;
    private readonly ILogger<AutoPlayService> logger;

    public AutoPlayService(
        MusicContext dbContext,
        IMusicPlayer musicPlayer,
        PlaylistService playlistService,
        RadioStreamService radioStreamService,
        PlayerStateService playerStateService,
        ILogger<AutoPlayService> logger)
    {
        this.dbContext = dbContext;
        this.musicPlayer = musicPlayer;
        this.playlistService = playlistService;
        this.radioStreamService = radioStreamService;
        this.playerStateService = playerStateService;
        this.logger = logger;
    }

    public async Task<AutoPlaySettingsViewModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await getOrCreateSettingsAsync(cancellationToken);
        return await mapSettingsAsync(settings, cancellationToken);
    }

    public async Task SaveSettingsAsync(AutoPlaySettingsViewModel request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await getOrCreateSettingsAsync(cancellationToken);
        var sources = request.Sources
            .Where(isValidSource)
            .DistinctBy(getSourceKey)
            .ToList();

        settings.VolumeLevel = Math.Clamp(request.VolumeLevel, 0, 100);
        settings.SilenceTimeoutMinutes = Math.Clamp(request.SilenceTimeoutMinutes, 1, 24 * 60);

        dbContext.AutoPlaySources.RemoveRange(settings.Sources);
        settings.Sources.Clear();

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            settings.Sources.Add(new AutoPlaySourceEntity
            {
                SourceType = source.SourceType,
                PlaylistName = source.PlaylistName?.Trim(),
                RadioStreamId = source.RadioStreamId,
                SortOrder = i
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryStartConfiguredAutoplayAsync(CancellationToken cancellationToken = default)
    {
        var settings = await getOrCreateSettingsAsync(cancellationToken);
        var sources = settings.Sources
            .OrderBy(source => source.SortOrder)
            .ToList();

        if (sources.Count == 0)
        {
            logger.LogInformation("Autoplay skipped because no sources are configured.");
            return false;
        }

        var candidates = await getCandidatesAsync(sources, cancellationToken);
        if (candidates.Count == 0)
        {
            logger.LogInformation("Autoplay skipped because no valid configured sources are available.");
            return false;
        }

        var candidate = candidates[Random.Shared.Next(candidates.Count)];
        var volumeLevel = Math.Clamp(settings.VolumeLevel, 0, 100);
        musicPlayer.SetVolume(volumeLevel);
        playerStateService.UpdateVolume(volumeLevel);
        musicPlayer.Stop();
        musicPlayer.ClearQueue();

        if (candidate.SourceType == AutoPlaySourceType.Playlist && candidate.PlaylistName != null)
        {
            logger.LogInformation("Autoplay starting shuffled playlist {PlaylistName} at volume {VolumeLevel}", candidate.PlaylistName, volumeLevel);
            await playlistService.PlayPlaylistAsync(candidate.PlaylistName, shuffleOverride: true);
            return true;
        }

        if (candidate.SourceType == AutoPlaySourceType.RadioStream && candidate.RadioStreamId.HasValue)
        {
            var stream = await radioStreamService.GetStreamByIdAsync(candidate.RadioStreamId.Value);
            if (stream == null)
            {
                logger.LogWarning("Autoplay stream {StreamId} no longer exists.", candidate.RadioStreamId.Value);
                return false;
            }

            logger.LogInformation("Autoplay starting stream {StreamName} at volume {VolumeLevel}", stream.Name, volumeLevel);
            musicPlayer.PlayStream(stream.Url, stream.Name);
            await radioStreamService.IncrementPlayCountAsync(stream.Id);
            return true;
        }

        return false;
    }

    private async Task<AutoPlaySettingsEntity> getOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.AutoPlaySettings
            .Include(item => item.Sources)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        settings = new AutoPlaySettingsEntity
        {
            VolumeLevel = 30,
            SilenceTimeoutMinutes = 30,
            Sources =
            [
                new AutoPlaySourceEntity
                {
                    SourceType = AutoPlaySourceType.Playlist,
                    PlaylistName = DefaultPlaylistName,
                    SortOrder = 0
                }
            ]
        };

        await dbContext.AutoPlaySettings.AddAsync(settings, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<AutoPlaySettingsViewModel> mapSettingsAsync(AutoPlaySettingsEntity settings, CancellationToken cancellationToken)
    {
        var streamIds = settings.Sources
            .Where(source => source.SourceType == AutoPlaySourceType.RadioStream && source.RadioStreamId.HasValue)
            .Select(source => source.RadioStreamId!.Value)
            .Distinct()
            .ToList();

        var streamNames = await dbContext.RadioStreams
            .Where(stream => streamIds.Contains(stream.Id))
            .ToDictionaryAsync(stream => stream.Id, stream => stream.Name, cancellationToken);

        return new AutoPlaySettingsViewModel
        {
            VolumeLevel = settings.VolumeLevel,
            SilenceTimeoutMinutes = settings.SilenceTimeoutMinutes,
            Sources = settings.Sources
                .OrderBy(source => source.SortOrder)
                .Select(source => new AutoPlaySourceViewModel
                {
                    Id = source.Id,
                    SourceType = source.SourceType,
                    PlaylistName = source.PlaylistName,
                    RadioStreamId = source.RadioStreamId,
                    DisplayName = getDisplayName(source, streamNames)
                })
                .ToList()
        };
    }

    private async Task<List<AutoPlaySourceViewModel>> getCandidatesAsync(
        List<AutoPlaySourceEntity> configuredSources,
        CancellationToken cancellationToken)
    {
        var playlistNames = configuredSources
            .Where(source => source.SourceType == AutoPlaySourceType.Playlist && !string.IsNullOrWhiteSpace(source.PlaylistName))
            .Select(source => source.PlaylistName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var radioStreamIds = configuredSources
            .Where(source => source.SourceType == AutoPlaySourceType.RadioStream && source.RadioStreamId.HasValue)
            .Select(source => source.RadioStreamId!.Value)
            .Distinct()
            .ToList();

        var playablePlaylists = await dbContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlistNames.Contains(playlist.Name))
            .Where(playlist => dbContext.PlaylistItems.Any(item => item.PlaylistId == playlist.Id))
            .Select(playlist => playlist.Name)
            .ToListAsync(cancellationToken);

        var playableStreams = await dbContext.RadioStreams
            .AsNoTracking()
            .Where(stream => radioStreamIds.Contains(stream.Id))
            .ToDictionaryAsync(stream => stream.Id, stream => stream.Name, cancellationToken);

        return configuredSources
            .Where(source =>
                (source.SourceType == AutoPlaySourceType.Playlist &&
                    source.PlaylistName != null &&
                    playablePlaylists.Contains(source.PlaylistName, StringComparer.OrdinalIgnoreCase)) ||
                (source.SourceType == AutoPlaySourceType.RadioStream &&
                    source.RadioStreamId.HasValue &&
                    playableStreams.ContainsKey(source.RadioStreamId.Value)))
            .Select(source => new AutoPlaySourceViewModel
            {
                Id = source.Id,
                SourceType = source.SourceType,
                PlaylistName = source.PlaylistName,
                RadioStreamId = source.RadioStreamId,
                DisplayName = getDisplayName(source, playableStreams)
            })
            .ToList();
    }

    private static bool isValidSource(AutoPlaySourceViewModel source)
    {
        return source.SourceType switch
        {
            AutoPlaySourceType.Playlist => !string.IsNullOrWhiteSpace(source.PlaylistName),
            AutoPlaySourceType.RadioStream => source.RadioStreamId.HasValue,
            _ => false
        };
    }

    private static string getSourceKey(AutoPlaySourceViewModel source)
    {
        return source.SourceType switch
        {
            AutoPlaySourceType.Playlist => $"playlist:{source.PlaylistName?.Trim().ToLowerInvariant()}",
            AutoPlaySourceType.RadioStream => $"stream:{source.RadioStreamId}",
            _ => string.Empty
        };
    }

    private static string getDisplayName(AutoPlaySourceEntity source, IReadOnlyDictionary<int, string> streamNames)
    {
        return source.SourceType switch
        {
            AutoPlaySourceType.Playlist => source.PlaylistName ?? "Playlist",
            AutoPlaySourceType.RadioStream when source.RadioStreamId.HasValue =>
                streamNames.TryGetValue(source.RadioStreamId.Value, out var streamName)
                    ? streamName
                    : $"Missing stream #{source.RadioStreamId.Value}",
            _ => "Unknown source"
        };
    }
}
