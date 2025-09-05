using CliWrap.Buffered;
using HomeSpeaker.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace HomeSpeaker.Server2;

public class LinuxSoxMusicPlayer : IMusicPlayer, IDisposable
{
    private readonly ILogger<LinuxSoxMusicPlayer> _logger;
    private readonly Mp3Library _library;
    private Process? playerProcess;
    private bool _disposed;

    public LinuxSoxMusicPlayer(ILogger<LinuxSoxMusicPlayer> logger, Mp3Library library)
    {
        _logger = logger;
        _library = library;
    }

    private PlayerStatus status = new();
    private Song? currentSong;
    public PlayerStatus Status => (status ?? new PlayerStatus()) with { CurrentSong = currentSong };

    private bool _startedPlaying;
    private Song? stoppedSong;

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
        playerProcess.StartInfo.FileName = "cvlc";
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

    public void PlaySong(Song song)
    {
        _startedPlaying = true;
        currentSong = song;
        stopPlaying();
        stoppedSong = null;

        playerProcess = new Process();
        playerProcess.StartInfo.FileName = "play";
        playerProcess.StartInfo.Arguments = $"\"{song.Path}\"";
        playerProcess.StartInfo.UseShellExecute = false;
        playerProcess.StartInfo.RedirectStandardOutput = true;
        playerProcess.StartInfo.RedirectStandardError = true;

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

        _logger.LogInformation($"Starting to play {song.Path}");
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
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
            _logger.LogWarning(ex, "Error cleaning up audio processes");
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
                _logger.LogInformation("Disposing LinuxSoxMusicPlayer");
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

    public async Task<int> GetVolume() =>
        (
            await CliWrap.Cli.Wrap("amixer")
                             .WithArguments("sget PCM,0")
                             .ExecuteBufferedAsync()
        ).StandardOutput
        .Split(Environment.NewLine)
        .First(l => l.Contains("Mono:"))
        .Split('[', ']', '%')
        .Skip(1)
        .Select(p => int.Parse(p))
        .First();

    public void SetVolume(int level0to100)
    {
        int actualMin = 40;
        int actualMax = 100;
        var percent = Math.Max(0, Math.Min(100, level0to100)) / 100M;
        var newLevel = (actualMax - actualMin) * percent + actualMin;
        _logger.LogInformation("Desired volume: {level0to100}; newLevel {newLevel} = (actualMax {actualMax} - actual Min {actualMin}) * percent {percent} + actualMin {actualMin}",
            level0to100, newLevel, actualMax, actualMin, percent, actualMin);
        Process.Start("amixer", $"sset PCM,0 {newLevel}%");
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

    public bool StillPlaying
    {
        get
        {
            _logger.LogInformation($"StillPlaying: startedPlaying {_startedPlaying} || (playerProcess?.HasExited {playerProcess?.HasExited} ?? true) {playerProcess?.HasExited ?? false} == false) {(playerProcess?.HasExited ?? true) == false}");
            return _startedPlaying || (playerProcess?.HasExited ?? true) == false;
        }
    }

    private readonly ConcurrentQueue<Song> _songQueue = new();

    public event EventHandler<string>? PlayerEvent;

    public IEnumerable<Song> SongQueue => _songQueue.ToArray();
}
