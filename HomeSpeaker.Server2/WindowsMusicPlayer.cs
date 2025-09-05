using HomeSpeaker.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HomeSpeaker.Server2;

public class WindowsMusicPlayer : IMusicPlayer, IDisposable
{
    public WindowsMusicPlayer(ILogger<WindowsMusicPlayer> logger, Mp3Library library)
    {
        _logger = logger;
        _library = library;
    }

    private const string _vlc = @"c:\program files\videolan\vlc\vlc.exe";
    private readonly ILogger<WindowsMusicPlayer> _logger;
    private readonly Mp3Library _library;
    private Process? playerProcess;
    private PlayerStatus status = new();
    private Song? currentSong;
    private Song? stoppedSong;
    private bool _disposed;
    public PlayerStatus Status => (status ?? new PlayerStatus()) with { CurrentSong = currentSong };

    private bool _startedPlaying;

    public void PlaySong(Song song)
    {
        currentSong = song;
        _startedPlaying = true;
        stopPlaying();
        stoppedSong = null;

        playerProcess = new Process();
        playerProcess.StartInfo.FileName = _vlc;
        playerProcess.StartInfo.Arguments = $"--play-and-exit \"{song.Path}\" --qt-start-minimized";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += (sender, args) => _logger.LogInformation("Vlc output data {data}", args.Data);

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

        _logger.LogInformation($"Starting to play {song.Path}");
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        playerProcess.Exited += PlayerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
        _startedPlaying = false;
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
                _logger.LogWarning(ex, "Error stopping player process");
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
            _logger.LogWarning(ex, "Error cleaning up VLC processes");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _logger.LogInformation("Disposing WindowsMusicPlayer");
                stopPlaying();
            }
            _disposed = true;
        }
    }

    private void PlayerProcess_Exited(object? sender, EventArgs e)
    {
        _logger.LogInformation("Finished playing a song.");
        currentSong = null;
        if (_songQueue.Count > 0)
        {
            playNextSongInQueue();
        }
        else
        {
            _logger.LogInformation("Nothing in the queue, so Status is now empty.");
            status = new PlayerStatus();
        }
    }
    private void playNextSongInQueue()
    {
        _logger.LogInformation($"There are still {_songQueue.Count} songs in the queue, so I'll play the next one:");
        if (_songQueue.TryDequeue(out var nextSong))
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
            _songQueue.Enqueue(song);
            story.AppendLine($"Added song# {song.SongId} to queue, now contains {_songQueue.Count} songs.");
        }
        else
        {
            story.AppendLine("Nothing playing, so instead of queuing I'll just play it...");
            PlaySong(song);
        }
        _logger.LogInformation(story.ToString());
    }

    public void ClearQueue()
    {
        _songQueue.Clear();
    }

    public void ResumePlay()
    {
        if (StillPlaying == false && stoppedSong != null)
        {
            PlaySong(stoppedSong);
        }
        else if (_songQueue.Any())
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
        _logger.LogInformation($"Asked to play stream: {streamPath}");

        //make a Uri first...to make sure the argument is a valid URL.
        //...maybe that helps a bit with unsafe input??
        var url = new Uri(streamPath).ToString();
        _logger.LogInformation($"After converting to a Uri: {streamPath}");

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
        playerProcess.StartInfo.FileName = _vlc;
        playerProcess.StartInfo.Arguments = $"\"{streamPath}\"";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;
        playerProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            _logger.LogInformation($"OutputDataReceived: {e.Data}");
        });
        playerProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            _logger.LogInformation($"ErrorDataReceived: {e.Data}");
        });
        _logger.LogInformation($"Starting vlc {streamPath}");
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        playerProcess.Exited += PlayerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
    }


    public void ShuffleQueue()
    {
        var oldQueue = _songQueue.ToList();
        _songQueue.Clear();
        foreach (var s in oldQueue.OrderBy(i => Guid.NewGuid()))
        {
            _songQueue.Enqueue(s);
        }
    }

    public void UpdateQueue(IEnumerable<string> songs)
    {
        _songQueue.Clear();
        foreach (var song in songs)
        {
            _songQueue.Enqueue(_library.Songs.Single(s => s.Path == song));
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
                var stillPlaying = _startedPlaying || (playerProcess?.HasExited ?? true) == false;
                _logger.LogInformation("startedPlaying {startedPlaying}, playerProcess {playerProcess}, stillPlaying {stillPlaying}", _startedPlaying, playerProcess, stillPlaying);
                return stillPlaying;
            }
            catch { return false; }
        }
    }

    private readonly ConcurrentQueue<Song> _songQueue = new();

    public event EventHandler<string>? PlayerEvent;

    public IEnumerable<Song> SongQueue => _songQueue.ToArray();
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
public static class Audio
{
    static IAudioEndpointVolume Vol()
    {
        var enumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
        if (enumerator == null)
            throw new InvalidOperationException("Failed to create device enumerator");
            
        IMMDevice? dev = null;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(/*eRender*/ 0, /*eMultimedia*/ 1, out dev));
        
        if (dev == null)
            throw new InvalidOperationException("Failed to get default audio endpoint");
            
        IAudioEndpointVolume? epv = null;
        var epvid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(dev.Activate(ref epvid, /*CLSCTX_ALL*/ 23, 0, out epv));
        
        return epv ?? throw new InvalidOperationException("Failed to activate audio endpoint volume");
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
