using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public class WindowsMusicPlayer : IMusicPlayer, IDisposable
{
    public WindowsMusicPlayer(ILogger<WindowsMusicPlayer> logger, Mp3Library library, ILoggerFactory loggerFactory)
    {
        this.logger = logger;
        this.library = library;
        icyReader = new IcyMetadataReader(loggerFactory.CreateLogger<IcyMetadataReader>());
        icyReader.TitleChanged += title =>
        {
            if (currentSong != null)
            {
                currentSong = currentSong with { Name = title };
            }
        };
    }

    private const string VlcPath = @"c:\program files\videolan\vlc\vlc.exe";
    private readonly ILogger<WindowsMusicPlayer> logger;
    private readonly Mp3Library library;
    private Process? playerProcess;
    private PlayerStatus status = new();
    private Song? currentSong;
    private Song? stoppedSong;
    private bool disposed;
    private DateTime? songStartTime;
    private TimeSpan songDuration;
    private bool isStream;
    private string? streamName;
    private readonly IcyMetadataReader icyReader;

    public PlayerStatus Status
    {
        get
        {
            var baseStatus = (status ?? new PlayerStatus()) with { CurrentSong = currentSong, IsStream = isStream, StreamName = streamName };
            if (!isStream && songStartTime.HasValue && songDuration > TimeSpan.Zero && currentSong != null)
            {
                var elapsed = DateTime.UtcNow - songStartTime.Value;
                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                if (elapsed > songDuration)
                {
                    elapsed = songDuration;
                }

                var remaining = songDuration - elapsed;
                var percentComplete = (decimal)(elapsed.TotalSeconds / songDuration.TotalSeconds);
                return baseStatus with { Elapsed = elapsed, Remaining = remaining, PercentComplete = percentComplete };
            }

            return baseStatus;
        }
    }

    private bool startedPlaying;

    public void PlaySong(Song song)
    {
        currentSong = song;
        isStream = false;
        startedPlaying = true;
        stopPlaying();
        stoppedSong = null;

        try
        {
            using var tagFile = TagLib.File.Create(song.Path);
            songDuration = tagFile.Properties.Duration;
        }
        catch
        {
            songDuration = TimeSpan.Zero;
        }

        playerProcess = new Process();
        playerProcess.StartInfo.FileName = VlcPath;
        playerProcess.StartInfo.Arguments = $"--play-and-exit \"{song.Path}\" --qt-start-minimized";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += (sender, args) => logger.LogInformation("Vlc output data {Data}", args.Data);

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

        status = new PlayerStatus { CurrentSong = currentSong, StillPlaying = true };

        logger.LogInformation("Starting to play {SongPath}", song.Path);
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        songStartTime = DateTime.UtcNow;
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

        songStartTime = null;
        songDuration = TimeSpan.Zero;
        icyReader.Stop();

        // Fallback: kill any remaining VLC processes that might be hanging around
        try
        {
            foreach (var proc in Process.GetProcessesByName("vlc"))
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
            logger.LogWarning(ex, "Error cleaning up VLC processes");
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
                logger.LogInformation("Disposing WindowsMusicPlayer");
                stopPlaying();
                sleepTimerCts?.Dispose();
                icyReader.Dispose();
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
            songStartTime = null;
            songDuration = TimeSpan.Zero;
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
        if (output != null)
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
            catch { }
        }

        playerStatus = new PlayerStatus();
        return false;
    }

    public void EnqueueSong(Song song)
    {
        var story = new StringBuilder($"Queuing up #{song.SongId} ({song.Path})\n");

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

        logger.LogInformation("Queue story: {Story}", story);
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

    public void SetVolume(int level0to100)
    {
        try
        {
            Audio.Volume = level0to100 / 100.0f;
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            logger.LogWarning("No default audio device found; cannot set volume.");
        }
    }

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
        playerProcess.StartInfo.FileName = VlcPath;
        playerProcess.StartInfo.Arguments = $"\"{streamUrl}\"";
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

    public Task<int> GetVolume()
    {
        try
        {
            return Task.FromResult((int)(Audio.Volume * 100));
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            logger.LogWarning("No default audio device found; returning volume as 0.");
            return Task.FromResult(0);
        }
    }

    public bool StillPlaying
    {
        get
        {
            try
            {
                var stillPlaying = startedPlaying || (playerProcess?.HasExited ?? true) == false;
                logger.LogInformation("startedPlaying {StartedPlaying}, playerProcess {PlayerProcess}, stillPlaying {StillPlaying}", startedPlaying, playerProcess, stillPlaying);
                return stillPlaying;
            }
            catch
            {
                return false;
            }
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
        ? sleepTimerEndTime.Value - DateTime.UtcNow.ToLocalTime()
        : null;

    public void SetSleepTimer(int minutes)
    {
        CancelSleepTimer();
        sleepTimerCts = new CancellationTokenSource();
        sleepTimerEndTime = DateTime.UtcNow.ToLocalTime().AddMinutes(minutes);

        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Sleep timer set for {Minutes} minutes", minutes);
                PlayerEvent?.Invoke(this, $"Sleep timer: {minutes} min");
                await Task.Delay(TimeSpan.FromMinutes(minutes), sleepTimerCts.Token);

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

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolume
{
    // F(), G(), ... are unused COM method slots. Define these if you care
    int F(); int G(); int H(); int I();
    int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
    int J();
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int K(); int L(); int M(); int N();
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
    int GetMute(out bool pbMute);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid id, int clsCtx, int activationParams, out IAudioEndpointVolume aev);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int F(); // Unused
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

public static class Audio
{
    public static IAudioEndpointVolume Vol()
    {
        var enumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
        if (enumerator == null)
        {
            throw new InvalidOperationException("Failed to create device enumerator");
        }

        IMMDevice? dev;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(/*eRender*/ 0, /*eMultimedia*/ 1, out dev));

        if (dev == null)
        {
            throw new InvalidOperationException("Failed to get default audio endpoint");
        }

        IAudioEndpointVolume? epv;
        var epvid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(dev.Activate(ref epvid, /*CLSCTX_ALL*/ 23, 0, out epv));

        return epv ?? throw new InvalidOperationException("Failed to activate audio endpoint volume");
    }
    public static float Volume
    {
        get
        {
            Marshal.ThrowExceptionForHR(Vol().GetMasterVolumeLevelScalar(out var v));
            return v;
        }
        set { Marshal.ThrowExceptionForHR(Vol().SetMasterVolumeLevelScalar(value, Guid.Empty)); }
    }
    public static bool Mute
    {
        get
        {
            Marshal.ThrowExceptionForHR(Vol().GetMute(out var mute));
            return mute;
        }
        set { Marshal.ThrowExceptionForHR(Vol().SetMute(value, Guid.Empty)); }
    }
}