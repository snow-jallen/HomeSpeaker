using System.Collections.Concurrent;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>The last board each anchor-home URL and range returned, shared by every page so a flaky home still shows something.</summary>
public sealed class RemoteAnchorCache
{
    private readonly ConcurrentDictionary<string, (AnchorBoardDto Board, DateTimeOffset At)> boards = new(StringComparer.Ordinal);

    public bool TryGet(string key, out AnchorBoardDto board, out DateTimeOffset at)
    {
        if (boards.TryGetValue(key, out var entry))
        {
            (board, at) = entry;
            return true;
        }

        board = default!;
        at = default;
        return false;
    }

    private readonly ConcurrentDictionary<string, DateTimeOffset> failures = new(StringComparer.Ordinal);

    public void Set(string key, AnchorBoardDto board, DateTimeOffset at)
    {
        boards[key] = (board, at);
        failures.TryRemove(key, out _);
    }

    /// <summary>Remember that the home didn't answer for this key, so callers can wait before asking again.</summary>
    public void MarkFailed(string key, DateTimeOffset at) => failures[key] = at;

    public bool FailedSince(string key, DateTimeOffset since) => failures.TryGetValue(key, out var at) && at >= since;

    /// <summary>Forget fresh copies after a change, but keep them as the fallback (marked old) if the home goes away.</summary>
    public void Expire()
    {
        foreach (var key in boards.Keys)
        {
            if (boards.TryGetValue(key, out var entry))
            {
                boards[key] = (entry.Board, DateTimeOffset.MinValue);
            }
        }
    }
}
