using System.Diagnostics;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Integrates with the <c>amazon-music</c> Python package (pip install amazon-music) to list
/// configured playlists and download their tracks for local playback.
/// <para>
/// Setup:  1. Install Python and the CLI: <c>pip install amazon-music</c><br/>
///         2. Obtain your token from https://amz.dezalty.com/login<br/>
///         3. Add your playlists to appsettings.json under "AmazonMusic"
/// </para>
/// </summary>
public class AmazonMusicService
{
    private readonly ILogger<AmazonMusicService> logger;
    private readonly IMusicPlayer player;
    private readonly Mp3Library mp3Library;
    private readonly IConfiguration configuration;
    private readonly string mediaFolder;

    public AmazonMusicService(
        ILogger<AmazonMusicService> logger,
        IMusicPlayer player,
        Mp3Library mp3Library,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.player = player;
        this.mp3Library = mp3Library;
        this.configuration = configuration;
        this.mediaFolder = configuration[ConfigKeys.MediaFolder] ?? "/music";
    }

    /// <summary>Returns the playlists configured in appsettings.json.</summary>
    public IEnumerable<AmazonPlaylistConfig> GetConfiguredPlaylists()
    {
        var playlists = new List<AmazonPlaylistConfig>();
        this.configuration.GetSection("AmazonMusic:Playlists").Bind(playlists);
        return playlists;
    }

    /// <summary>
    /// Returns true when the <c>amz</c> CLI is available on the host's PATH.
    /// On Linux/Raspberry Pi: <c>pip install amazon-music</c> then <c>amz --help</c>.
    /// </summary>
    public bool IsCliAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "amz",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns true when at least one playlist is configured in appsettings.</summary>
    public bool IsConfigured() => GetConfiguredPlaylists().Any();

    /// <summary>
    /// Downloads every track in the specified playlist into a per-playlist sub-folder of the
    /// media directory, then shuffles and enqueues them for immediate playback.
    /// </summary>
    public async Task<(bool Success, string Message)> PlayAmazonPlaylistAsync(string playlistId)
    {
        var playlist = GetConfiguredPlaylists()
            .FirstOrDefault(p => p.Id == playlistId);

        if (playlist is null)
        {
            return (false, $"Playlist '{playlistId}' not found in configuration.");
        }

        this.logger.LogInformation("Starting Amazon Music download for playlist '{Name}' ({Url})",
            playlist.Name, playlist.Url);

        var downloadDir = Path.Combine(this.mediaFolder, "AmazonMusic", sanitizeFolderName(playlist.Name));
        Directory.CreateDirectory(downloadDir);

        var token = this.configuration["AmazonMusic:Token"] ?? string.Empty;
        var quality = this.configuration["AmazonMusic:Quality"] ?? "Normal";

        // Build the amz CLI invocation
        // amz <url> -q <quality> -o <output-dir> [--token <token>]
        var args = $"\"{playlist.Url}\" -q {quality} -o \"{downloadDir}\"";
        if (!string.IsNullOrWhiteSpace(token))
        {
            args += $" --token \"{token}\"";
        }

        this.logger.LogInformation("Running: amz {Args}", args);

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "amz",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    this.logger.LogDebug("[amz] {Line}", e.Data);
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    this.logger.LogWarning("[amz err] {Line}", e.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Allow up to 10 minutes for downloads
            await Task.Run(() => proc.WaitForExit(600_000));

            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                return (false, "Download timed out after 10 minutes.");
            }

            if (proc.ExitCode != 0)
            {
                return (false, $"amz exited with code {proc.ExitCode}. Ensure your token is valid.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            this.logger.LogError(ex, "amz CLI not found or failed to start");
            return (false,
                "The 'amz' CLI was not found. Install it with: pip install amazon-music");
        }

        // Refresh the library so newly downloaded tracks are discoverable
        this.mp3Library.SyncLibrary();

        // Find the downloaded songs in the library and shuffle-play them
        var downloadedSongs = this.mp3Library.Songs
            .Where(s => s.Path.StartsWith(downloadDir, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (downloadedSongs.Count == 0)
        {
            // Fallback: scan the directory directly for audio files
            var audioFiles = Directory
                .GetFiles(downloadDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".opus", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (audioFiles.Count == 0)
            {
                return (false, "Download succeeded but no audio files were found in the output directory.");
            }

            this.logger.LogInformation(
                "Playing {Count} tracks from filesystem (library sync may be in progress)", audioFiles.Count);

            this.player.Stop();
            foreach (var path in audioFiles.OrderBy(_ => Random.Shared.Next()))
            {
                // Play files directly by path since the library hasn't indexed them yet
                var song = new Song { Path = path, Name = Path.GetFileNameWithoutExtension(path) };
                this.player.EnqueueSong(song);
            }

            return (true,
                $"Now playing {audioFiles.Count} tracks from '{playlist.Name}' (shuffled).");
        }

        this.player.Stop();
        foreach (var song in downloadedSongs.OrderBy(_ => Random.Shared.Next()))
        {
            this.player.EnqueueSong(song);
        }

        this.logger.LogInformation(
            "Enqueued {Count} shuffled tracks from Amazon playlist '{Name}'",
            downloadedSongs.Count, playlist.Name);

        return (true, $"Now playing {downloadedSongs.Count} tracks from '{playlist.Name}' (shuffled).");
    }

    private static string sanitizeFolderName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Trim();
}

/// <summary>A single Amazon Music playlist entry from appsettings.json.</summary>
public class AmazonPlaylistConfig
{
    /// <summary>A short unique ID used internally (e.g. "favorites").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in the UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full Amazon Music playlist URL, e.g. https://music.amazon.com/playlists/B0FBL3CC8M</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional: approximate number of tracks (used for display only).</summary>
    public int TrackCount { get; set; }
}
