using System.Text.Json;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Remembers the last board it saw and says whether a new one differs in anything a page shows.</summary>
public sealed class AnchorBoardWatcher
{
    private string? last;

    public bool Changed(AnchorBoardDto board)
    {
        var fingerprint = JsonSerializer.Serialize(board with { Stale = false });
        var changed = last is not null && !string.Equals(last, fingerprint, StringComparison.Ordinal);
        last = fingerprint;
        return changed;
    }
}
