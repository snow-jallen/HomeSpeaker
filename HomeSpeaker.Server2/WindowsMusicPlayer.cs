using HomeSpeaker.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HomeSpeaker.Server;

public class WindowsMusicPlayer : IMusicPlayer, IDisposable
{
    public WindowsMusicPlayer(ILogger<WindowsMusicPlayer> logger, Mp3Library library)
    {
        this.logger = logger;
        this.library = library;
    }

    const string vlc = @"c:\program files\videolan\vlc\vlc.exe";
    private readonly ILogger<WindowsMusicPlayer> logger;
    private readonly Mp3Library library;
    private Process? playerProcess;
    private PlayerStatus status = new();
    private Song? currentSong;
    private Song? stoppedSong;
    private bool disposed = false;
    public PlayerStatus Status => (status ?? new PlayerStatus()) with { CurrentSong = currentSong };

    private bool startedPlaying = false;

    public void PlaySong(Song song)
    {
        currentSong = song;
        startedPlaying = true;
        stopPlaying();
        stoppedSong = null;

        playerProcess = new Process();
        playerProcess.StartInfo.FileName = vlc;
        playerProcess.StartInfo.Arguments = $"--play-and-exit \"{song.Path}\" --qt-start-minimized";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += (sender, args) => logger.LogInformation("Vlc output data {data}", args.Data);

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
            else
            {
                this.status = new PlayerStatus();
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
            else
            {
                this.status = new PlayerStatus();
            }
        });

        status = new PlayerStatus { CurrentSong = currentSong, StillPlaying = true };

        logger.LogInformation($"Starting to play {song.Path}");
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        playerProcess.Exited += PlayerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
        startedPlaying = false;
    }    private void stopPlaying()
    {
        if (playerProcess != null)
        {
            try
            {
                if (!playerProcess.HasExited)
                {
                    playerProcess.Exited -= PlayerProcess_Exited; // Stop listening to when the process ends
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
            }
            disposed = true;
        }
    }

    private void PlayerProcess_Exited(object? sender, EventArgs e)
    {
        logger.LogInformation("Finished playing a song.");
        currentSong = null;
        if (songQueue.Count > 0)
        {
            playNextSongInQueue();
        }
        else
        {
            logger.LogInformation("Nothing in the queue, so Status is now empty.");
            status = new PlayerStatus();
        }
    }
    private void playNextSongInQueue()
    {
        logger.LogInformation($"There are still {songQueue.Count} songs in the queue, so I'll play the next one:");
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
                var parts = output.Split(new char[] { ' ', '%', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
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
        logger.LogInformation(story.ToString());
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
    }

    public void SetVolume(int level)
    {
        Audio.Volume = (level / 100.0f);
    }

    public void PlayStream(string streamPath)
    {
        logger.LogInformation($"Asked to play stream: {streamPath}");

        //make a Uri first...to make sure the argument is a valid URL.
        //...maybe that helps a bit with unsafe input??
        var url = new Uri(streamPath).ToString();
        logger.LogInformation($"After converting to a Uri: {streamPath}");

        stopPlaying();
        status = new PlayerStatus
        {
            CurrentSong = new Song
            {
                Album = url,
                Artist = url,
                Name = url,
                Path = url
            }
        };
        playerProcess = new Process();
        playerProcess.StartInfo.FileName = vlc;
        playerProcess.StartInfo.Arguments = $"\"{streamPath}\"";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            logger.LogInformation($"OutputDataReceived: {e.Data}");
        });
        playerProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            logger.LogInformation($"ErrorDataReceived: {e.Data}");
        });
        logger.LogInformation($"Starting vlc {streamPath}");
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        playerProcess.Exited += PlayerProcess_Exited;

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
        return Task.FromResult((int)(Audio.Volume * 100));
    }

    public bool StillPlaying
    {
        get
        {
            try
            {
                var stillPlaying = startedPlaying || (playerProcess?.HasExited ?? true) == false;
                logger.LogInformation("startedPlaying {startedPlaying}, playerProcess {playerProcess}, stillPlaying {stillPlaying}", startedPlaying, playerProcess, stillPlaying);
                return stillPlaying;
            }
            catch { return false; }
        }
    }

    private ConcurrentQueue<Song> songQueue = new ConcurrentQueue<Song>();

    public event EventHandler<string>? PlayerEvent;

    public IEnumerable<Song> SongQueue => songQueue.ToArray();
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    // f(), g(), ... are unused COM method slots. Define these if you care
    int f(); int g(); int h(); int i();
    int SetMasterVolumeLevelScalar(float fLevel, System.Guid pguidEventContext);
    int j();
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int k(); int l(); int m(); int n();
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, System.Guid pguidEventContext);
    int GetMute(out bool pbMute);
}
[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    int Activate(ref System.Guid id, int clsCtx, int activationParams, out IAudioEndpointVolume aev);
}
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    int f(); // Unused
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorComObject { }
public class Audio
{
    static IAudioEndpointVolume Vol()
    {
        var enumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
        IMMDevice dev = null;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(/*eRender*/ 0, /*eMultimedia*/ 1, out dev));
        IAudioEndpointVolume epv = null;
        var epvid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(dev.Activate(ref epvid, /*CLSCTX_ALL*/ 23, 0, out epv));
        return epv;
    }
    public static float Volume
    {
        get { float v = -1; Marshal.ThrowExceptionForHR(Vol().GetMasterVolumeLevelScalar(out v)); return v; }
        set { Marshal.ThrowExceptionForHR(Vol().SetMasterVolumeLevelScalar(value, System.Guid.Empty)); }
    }
    public static bool Mute
    {
        get { bool mute; Marshal.ThrowExceptionForHR(Vol().GetMute(out mute)); return mute; }
        set { Marshal.ThrowExceptionForHR(Vol().SetMute(value, System.Guid.Empty)); }
    }
}
