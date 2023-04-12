using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace HomeSpeaker.Server;

public class LinuxSoxMusicPlayer : IMusicPlayer
{
    private readonly ILogger<LinuxSoxMusicPlayer> logger;
    private readonly Mp3Library library;
    private Process? playerProcess;

    public LinuxSoxMusicPlayer(ILogger<LinuxSoxMusicPlayer> logger, Mp3Library library)
    {
        this.logger = logger;
        this.library = library;
    }

    private PlayerStatus status = new();
    private Song? currentSong;
    public PlayerStatus Status => (status ?? new PlayerStatus()) with { CurrentSong = currentSong };

    private bool startedPlaying = false;
    private Song? stoppedSong;

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
        playerProcess.StartInfo.FileName = "cvlc";
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

    public void PlaySong(Song song)
    {
        startedPlaying = true;
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
                return;

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
                return;

            if (TryParsePlayerOutput(e.Data, out var status))
            {
                this.status = status;
            }
            else
            {
                this.status = new PlayerStatus();
            }
        });

        logger.LogInformation($"Starting to play {song.Path}");
        playerProcess.EnableRaisingEvents = true;
        playerProcess.Start();
        PlayerEvent?.Invoke(this, "Playing " + song.Name);
        playerProcess.Exited += PlayerProcess_Exited;

        playerProcess.BeginOutputReadLine();
        playerProcess.BeginErrorReadLine();
        startedPlaying = false;
    }

    private void stopPlaying()
    {
        if (playerProcess != null && playerProcess.HasExited is false)
            playerProcess.Exited -= PlayerProcess_Exited;//stop listening to when the process ends.

        foreach (var proc in Process.GetProcessesByName("play").Union(Process.GetProcessesByName("vlc")))
            proc.Kill();
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

    public void SetVolume(int level0to100)
    {
        int actualMin = 40;
        int actualMax = 100;
        var percent = Math.Max(0, Math.Min(100, level0to100)) / 100M;
        var newLevel = (actualMax - actualMin) * percent + actualMin;
        logger.LogInformation("Desired volume: {level0to100}; newLevel {newLevel} = (actualMax {actualMax} - actual Min {actualMin}) * percent {percent} + actualMin {actualMin}",
            level0to100, newLevel, actualMax, actualMin, percent, actualMin);
        Process.Start("amixer", $"--card 0 sset Headphone,0 {newLevel}%");
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

    public bool StillPlaying
    {
        get
        {
            logger.LogInformation($"StillPlaying: startedPlaying {startedPlaying} || (playerProcess?.HasExited {playerProcess?.HasExited} ?? true) {playerProcess?.HasExited ?? false} == false) {(playerProcess?.HasExited ?? true) == false}");
            return startedPlaying || (playerProcess?.HasExited ?? true) == false;
        }
    }

    private ConcurrentQueue<Song> songQueue = new ConcurrentQueue<Song>();

    public event EventHandler<string>? PlayerEvent;

    public IEnumerable<Song> SongQueue => songQueue.ToArray();
}
