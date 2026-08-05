using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using CliWrap.Buffered;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public class LinuxSoxMusicPlayer : IMusicPlayer, IDisposable
{
    private readonly ILogger<LinuxSoxMusicPlayer> logger;
    private readonly Mp3Library library;
    private readonly AudioDeviceDetector audioDeviceDetector;
    private readonly TimeProvider timeProvider;
    private Process? playerProcess;
    private bool disposed;
    private bool deviceDetectionComplete;

    // Volume set/get coordination. amixer only offers ~1% granularity over the
    // 40-100 usable range, so the 0-100 slider can't be losslessly round-tripped
    // through ALSA for every value (61 raw steps can't represent 101 slider
    // positions). Caching the slider value we last successfully wrote lets
    // GetVolume() return that exact value back when the hardware still reads the
    // same raw level, instead of a quantization-rounded neighbor - which is what
    // made the UI slider visibly "snap" a step within a couple seconds of being
    // dragged. When the raw level no longer matches (e.g. the physical volume
    // knob was turned), we fall through to recomputing from the raw reading so
    // real external changes still show up.
    private readonly SemaphoreSlim volumeSetLock = new(1, 1);
    private int volumeRequestVersion;
    private int appliedVolumeVersion;
    private int lastRequestedSliderLevel = -1;
    private int? lastSetActualLevel;
    private int? lastSetSliderLevel;
    private int consecutiveAmixerFailures;
    private bool redetectInProgress;

    public LinuxSoxMusicPlayer(ILogger<LinuxSoxMusicPlayer> logger, Mp3Library library, ILoggerFactory loggerFactory, AudioDeviceDetector audioDeviceDetector, TimeProvider timeProvider)
    {
        this.logger = logger;
        this.library = library;
        this.audioDeviceDetector = audioDeviceDetector;
        this.timeProvider = timeProvider;
        icyReader = new IcyMetadataReader(loggerFactory.CreateLogger<IcyMetadataReader>());
        icyReader.TitleChanged += title =>
        {
            if (currentSong != null)
            {
                currentSong = currentSong with { Name = title };
            }
        };

        // Start device detection in background
        _ = initializeAudioDeviceAsync();
    }

    private async Task initializeAudioDeviceAsync()
    {
        try
        {
            await audioDeviceDetector.DetectAndSelectDeviceAsync();
            deviceDetectionComplete = true;
            logger.LogInformation("Audio device initialization complete. Using card: {Card}, mixer: {Mixer}",
                audioDeviceDetector.SelectedCard ?? "(default)",
                audioDeviceDetector.SelectedMixerControl ?? "(default)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize audio device, will use system defaults");
            deviceDetectionComplete = true;
        }
    }

    private async Task ensureDeviceDetectedAsync()
    {
        // Wait up to 5 seconds for device detection to complete
        var timeout = timeProvider.GetUtcNow().AddSeconds(5);
        while (!deviceDetectionComplete && timeProvider.GetUtcNow() < timeout)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeProvider, CancellationToken.None);
        }
    }

    private PlayerStatus status = new();
    private Song? currentSong;
    private bool isStream;
    private string? streamName;
    private readonly IcyMetadataReader icyReader;
    public PlayerStatus Status => (status ?? new PlayerStatus()) with { CurrentSong = currentSong, IsStream = isStream, StreamName = streamName };

    private bool startedPlaying;
    private Song? stoppedSong;

    public void PlayStream(string streamUrl, string? name = null)
    {
        logger.LogInformation("Asked to play stream: {StreamUrl}", streamUrl);

        //make a Uri first...to make sure the argument is a valid URL.
        //...maybe that helps a bit with unsafe input??
        var url = new Uri(streamUrl).ToString();
        logger.LogInformation("After converting to a Uri: {StreamUrl}", url);

        stopPlaying();
        currentSong = new Song { Name = name ?? url, Path = url };
        isStream = true;
        streamName = name ?? url;
        status = new PlayerStatus { StillPlaying = true };
        icyReader.Start(url);
        playerProcess = new Process();
        playerProcess.StartInfo.FileName = "cvlc";

        // Use detected audio device for VLC via ALSA
        var vlcAudioArgs = audioDeviceDetector.SelectedCard != null
            ? $"--aout=alsa --alsa-audio-device=hw:{audioDeviceDetector.SelectedCard}"
            : "";
        playerProcess.StartInfo.Arguments = $"{vlcAudioArgs} \"{streamUrl}\"";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            logger.LogInformation("OutputDataReceived: {Data}", e.Data);
        });
        playerProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            logger.LogInformation("ErrorDataReceived: {Data}", e.Data);
        });
        logger.LogInformation("Starting vlc {StreamUrl}", streamUrl);
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        playerProcess.Exited += playerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
    }

    public void PlaySong(Song song)
    {
        startedPlaying = true;
        currentSong = song;
        isStream = false;
        stopPlaying();
        stoppedSong = null;

        playerProcess = new Process();
        playerProcess.StartInfo.FileName = "play";
        playerProcess.StartInfo.Arguments = $"\"{song.Path}\"";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;

        // Set the audio device for sox/play via AUDIODEV environment variable
        if (audioDeviceDetector.SelectedCard != null)
        {
            playerProcess.StartInfo.EnvironmentVariables["AUDIODEV"] = $"hw:{audioDeviceDetector.SelectedCard}";
            logger.LogInformation("Using audio device: hw:{Card}", audioDeviceDetector.SelectedCard);
        }

        playerProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            if (e?.Data == null)
            {
                return;
            }

            if (TryParsePlayerOutput(e.Data, out var status))
            {
                this.status = status;
            }
        });
        playerProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            if (e?.Data == null)
            {
                return;
            }

            if (TryParsePlayerOutput(e.Data, out var status))
            {
                this.status = status;
            }
        });

        logger.LogInformation("Starting to play {SongPath}", song.Path);
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
        playerProcess.Exited += playerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
        startedPlaying = false;
    }
    private void stopPlaying()
    {
        if (playerProcess != null)
        {
            try
            {
                if (!playerProcess.HasExited)
                {
                    playerProcess.Exited -= playerProcess_Exited; // Stop listening to when the process ends
                    playerProcess.Kill();
                    playerProcess.WaitForExit(5000); // Wait up to 5 seconds for clean exit
                }

                playerProcess.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping player process");
            }
            finally
            {
                playerProcess = null;
            }
        }

        icyReader.Stop();

        // Fallback: kill any remaining processes that might be hanging around
        try
        {
            foreach (var proc in Process.GetProcessesByName("play").Union(Process.GetProcessesByName("vlc")))
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(2000);
                }

                proc.Dispose();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error cleaning up audio processes");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                logger.LogInformation("Disposing LinuxSoxMusicPlayer");
                stopPlaying();
                sleepTimerCts?.Dispose();
                icyReader.Dispose();
                volumeSetLock.Dispose();
            }

            disposed = true;
        }
    }

    private void playerProcess_Exited(object? sender, EventArgs e)
    {
        logger.LogInformation("Finished playing a song.");
        lastPlayedSong = currentSong;
        currentSong = null;

        if (songQueue.Any())
        {
            playNextSongInQueue();
        }
        else if (repeatMode && lastPlayedSong != null)
        {
            logger.LogInformation("Repeat mode on, replaying last song: {SongName}", lastPlayedSong.Name);
            PlaySong(lastPlayedSong);
        }
        else
        {
            logger.LogInformation("Nothing in the queue, so Status is now empty.");
            status = new PlayerStatus();
            isStream = false;
        }
    }

    private void playNextSongInQueue()
    {
        logger.LogInformation("There are still {QueueCount} songs in the queue, so I'll play the next one:", songQueue.Count);
        if (songQueue.TryDequeue(out var nextSong))
        {
            PlaySong(nextSong);
        }
    }

    public static bool TryParsePlayerOutput(string output, out PlayerStatus playerStatus)
    {
        try
        {
            var parts = output.Split(IMusicPlayer.Separators, StringSplitOptions.RemoveEmptyEntries);
            var percentComplete = decimal.Parse(parts[0].Substring(parts[0].IndexOf(':') + 1)) / 100;
            var elapsedTime = TimeSpan.Parse(parts[1]);
            var remainingTime = TimeSpan.Parse(parts[2]);
            playerStatus = new PlayerStatus
            {
                Elapsed = elapsedTime,
                PercentComplete = percentComplete,
                Remaining = remainingTime,
                StillPlaying = true
            };
            return true;
        }
        catch
        {
            playerStatus = new PlayerStatus();
            return false;
        }
    }

    public void EnqueueSong(Song song)
    {
        var story = new StringBuilder($"Queuing up {song.Path}\n");

        if (StillPlaying)
        {
            story.AppendLine("StillPlaying is true, so I'll add to queue.");
            songQueue.Enqueue(song);
            story.AppendLine($"Added song# {song.SongId} to queue, now contains {songQueue.Count} songs.");
        }
        else
        {
            story.AppendLine("Nothing playing, so instead of queuing I'll just play it...");
            PlaySong(song);
        }

        logger.LogInformation("Enqueued song: {Story}", story);
    }

    public void ClearQueue()
    {
        songQueue.Clear();
    }

    public void ResumePlay()
    {
        if (StillPlaying == false && stoppedSong != null)
        {
            PlaySong(stoppedSong);
        }
        else if (songQueue.Any())
        {
            playNextSongInQueue();
        }
    }

    public void SkipToNext()
    {
        Stop();
        playNextSongInQueue();
    }

    public void Stop()
    {
        stoppedSong = currentSong;
        stopPlaying();
        currentSong = null;
        status = new PlayerStatus();
        isStream = false;
        streamName = null;
    }

    public async Task<int> GetVolume()
    {
        // While a SetVolume is still in flight, report the level that was just
        // requested instead of the soon-to-be-stale hardware reading. Clients
        // poll status every couple seconds; reading amixer mid-set would hand
        // them the old level and make their sliders snap back before jumping
        // to the new value.
        var requested = Volatile.Read(ref lastRequestedSliderLevel);
        if (requested >= 0 && Volatile.Read(ref appliedVolumeVersion) != Volatile.Read(ref volumeRequestVersion))
        {
            return requested;
        }

        await ensureDeviceDetectedAsync();

        var card = audioDeviceDetector.SelectedCard ?? "0";
        var mixer = audioDeviceDetector.SelectedMixerControl ?? "PCM";

        try
        {
            var result = await CliWrap.Cli.Wrap("amixer")
                                 .WithArguments($"-c {card} sget {mixer}")
                                 .WithValidation(CliWrap.CommandResultValidation.None)
                                  .ExecuteBufferedAsync(CancellationToken.None);

            if (result.ExitCode != 0)
            {
                logger.LogWarning("amixer sget exited with code {Code} for card {Card} mixer {Mixer}: {Error}",
                    result.ExitCode, card, mixer, result.StandardError);
                await noteAmixerFailureAsync();
                return lastSetSliderLevel ?? 50;
            }

            // Try to find a line with volume percentage
            var lines = result.StandardOutput.Split(Environment.NewLine);
            var volumeLine = lines.FirstOrDefault(l => l.Contains("Mono:"))
                          ?? lines.FirstOrDefault(l => l.Contains("Front Left:"))
                          ?? lines.FirstOrDefault(l => l.Contains('%'));

            if (volumeLine != null)
            {
                var actual = volumeLine
                    .Split('[', ']', '%')
                    .Skip(1)
                    .Select(p => int.TryParse(p, out var v) ? v : -1)
                    .FirstOrDefault(v => v >= 0);

                consecutiveAmixerFailures = 0;

                // If the raw ALSA level still matches what we last wrote, hand back the
                // exact slider value we set rather than re-deriving it - the 40-100 raw
                // range can't represent all 101 slider positions, so re-deriving would
                // sometimes land one step off and make the UI slider jitter on its own.
                // If the raw level has moved (e.g. someone turned the physical knob),
                // fall through and recompute so the real change is reflected.
                if (lastSetActualLevel.HasValue && actual == lastSetActualLevel.Value)
                {
                    return lastSetSliderLevel!.Value;
                }

                var slider = ActualToSlider(actual);
                lastSetActualLevel = actual;
                lastSetSliderLevel = slider;
                return slider;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get volume from card {Card} mixer {Mixer}", card, mixer);
            await noteAmixerFailureAsync();
        }

        return lastSetSliderLevel ?? 50; // Default fallback
    }

    public void SetVolume(int level0to100)
    {
        Volatile.Write(ref lastRequestedSliderLevel, Math.Max(0, Math.Min(100, level0to100)));
        var myVersion = Interlocked.Increment(ref volumeRequestVersion);
        _ = setVolumeAsync(level0to100, myVersion);
    }

    private async Task setVolumeAsync(int level0to100, int myVersion)
    {
        await ensureDeviceDetectedAsync();

        await volumeSetLock.WaitAsync(CancellationToken.None);
        try
        {
            // A newer SetVolume call already arrived while we were waiting our turn
            // (e.g. the user dragged the slider several times in a row) - only the
            // most recent request should actually reach amixer.
            if (myVersion != Volatile.Read(ref volumeRequestVersion))
            {
                return;
            }

            var card = audioDeviceDetector.SelectedCard ?? "0";
            var mixer = audioDeviceDetector.SelectedMixerControl ?? "PCM";
            var slider = Math.Max(0, Math.Min(100, level0to100));
            var actualLevel = SliderToActual(slider);

            logger.LogInformation("Setting volume on card {Card} mixer {Mixer}: {Slider}% (slider) -> {Actual}% (amixer)",
                card, mixer, slider, actualLevel);

            try
            {
                var result = await CliWrap.Cli.Wrap("amixer")
                                     .WithArguments($"-c {card} sset {mixer} {actualLevel}%")
                                     .WithValidation(CliWrap.CommandResultValidation.None)
                                     .ExecuteBufferedAsync(CancellationToken.None);

                if (result.ExitCode == 0)
                {
                    consecutiveAmixerFailures = 0;
                    lastSetActualLevel = actualLevel;
                    lastSetSliderLevel = slider;
                }
                else
                {
                    logger.LogWarning("amixer sset exited with code {Code} for card {Card} mixer {Mixer}: {Error}",
                        result.ExitCode, card, mixer, result.StandardError);
                    await noteAmixerFailureAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to set volume on card {Card} mixer {Mixer}", card, mixer);
                await noteAmixerFailureAsync();
            }

            // Whether or not amixer succeeded, this request is no longer in
            // flight - GetVolume should go back to trusting hardware reads.
            Volatile.Write(ref appliedVolumeVersion, myVersion);
        }
        finally
        {
            volumeSetLock.Release();
        }
    }

    // amixer failures usually mean the card we auto-detected at startup no longer
    // exists - most commonly because the USB speaker was unplugged/replugged (or
    // reset by its hub) and ALSA renumbered the cards. Re-running detection lets
    // us recover without requiring a restart.
    private async Task noteAmixerFailureAsync()
    {
        if (Interlocked.Increment(ref consecutiveAmixerFailures) < 3 || redetectInProgress)
        {
            return;
        }

        redetectInProgress = true;
        try
        {
            logger.LogWarning("Repeated amixer failures on card {Card}; re-running audio device detection",
                audioDeviceDetector.SelectedCard);
            await audioDeviceDetector.DetectAndSelectDeviceAsync();
            consecutiveAmixerFailures = 0;
            lastSetActualLevel = null;
            lastSetSliderLevel = null;
            logger.LogInformation("Re-detected audio device: card={Card} mixer={Mixer}",
                audioDeviceDetector.SelectedCard, audioDeviceDetector.SelectedMixerControl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to re-detect audio device after repeated amixer failures");
        }
        finally
        {
            redetectInProgress = false;
        }
    }

    // The speaker is inaudible below ~40% on the ALSA mixer, so the 0-100 slider
    // presented to clients is mapped onto the usable 40-100 ALSA range. The two
    // mappings below are exact inverses so a value written by SetVolume reads back
    // unchanged through GetVolume (otherwise the slider snaps up after each set).
    private const int VolumeFloor = 40;

    internal static int SliderToActual(int slider0to100)
    {
        var slider = Math.Max(0, Math.Min(100, slider0to100));
        return (int)Math.Round(VolumeFloor + (100 - VolumeFloor) * (slider / 100.0));
    }

    internal static int ActualToSlider(int actual0to100)
    {
        var actual = Math.Max(0, Math.Min(100, actual0to100));
        var slider = (actual - VolumeFloor) * 100.0 / (100 - VolumeFloor);
        return (int)Math.Round(Math.Max(0, Math.Min(100, slider)));
    }

    public void ShuffleQueue()
    {
        var oldQueue = songQueue.ToList();
        songQueue.Clear();
        foreach (var s in oldQueue.OrderBy(i => Guid.NewGuid()))
        {
            songQueue.Enqueue(s);
        }
    }

    public void UpdateQueue(IEnumerable<string> songs)
    {
        songQueue.Clear();
        foreach (var song in songs)
        {
            songQueue.Enqueue(library.Songs.Single(s => s.Path == song));
        }
    }

    public bool StillPlaying
    {
        get
        {
            logger.LogDebug("StillPlaying: startedPlaying {StartedPlaying} || (playerProcess?.HasExited {HasExited} ?? true) == false) {(playerProcess?.HasExited ?? true) == false}",
                startedPlaying,
                playerProcess?.HasExited,
                playerProcess?.HasExited ?? false);

            return startedPlaying || (playerProcess?.HasExited ?? true) == false;
        }
    }

    private readonly ConcurrentQueue<Song> songQueue = new();
    private Song? lastPlayedSong;
    private bool repeatMode;
    private CancellationTokenSource? sleepTimerCts;
    private DateTime? sleepTimerEndTime;

    public event EventHandler<string>? PlayerEvent;

    public IEnumerable<Song> SongQueue => songQueue.ToArray();

    public bool RepeatMode
    {
        get => repeatMode;
        set
        {
            repeatMode = value;
            logger.LogInformation("Repeat mode set to {RepeatMode}", value);
            PlayerEvent?.Invoke(this, value ? "Repeat mode: ON" : "Repeat mode: OFF");
        }
    }

    public bool SleepTimerActive => sleepTimerCts != null && !sleepTimerCts.IsCancellationRequested;

    public TimeSpan? SleepTimerRemaining => sleepTimerEndTime.HasValue
        ? sleepTimerEndTime.Value - timeProvider.GetUtcNow().UtcDateTime
        : null;

    public void SetSleepTimer(int minutes)
    {
        CancelSleepTimer();
        sleepTimerCts = new CancellationTokenSource();
        sleepTimerEndTime = timeProvider.GetUtcNow().AddMinutes(minutes).UtcDateTime;

        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Sleep timer set for {Minutes} minutes", minutes);
                PlayerEvent?.Invoke(this, $"Sleep timer: {minutes} min");
                await Task.Delay(TimeSpan.FromMinutes(minutes), timeProvider, sleepTimerCts.Token);

                logger.LogInformation("Sleep timer expired, stopping playback");
                Stop();
                ClearQueue();
                PlayerEvent?.Invoke(this, "Sleep timer: stopped");
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Sleep timer cancelled");
            }
            finally
            {
                sleepTimerEndTime = null;
                sleepTimerCts?.Dispose();
                sleepTimerCts = null;
            }
        }, sleepTimerCts.Token);
    }

    public void CancelSleepTimer()
    {
        if (sleepTimerCts != null)
        {
            sleepTimerCts.Cancel();
            sleepTimerCts.Dispose();
            sleepTimerCts = null;
            sleepTimerEndTime = null;
            logger.LogInformation("Sleep timer cancelled");
            PlayerEvent?.Invoke(this, "Sleep timer: cancelled");
        }
    }
}
