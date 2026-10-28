using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests;

public class Mp3LibraryTests
{
    private const int FileCount = 20;

    [Fact]
    public async Task Songs_ManyConcurrentReadersWhileDirty_RescanOnce()
    {
        var parser = new SlowTagParser(TimeSpan.FromMilliseconds(5));
        using var library = createLibrary(parser);
        parser.Reset();

        library.MarkDirty("test");
        var readers = Enumerable.Range(0, 16).Select(_ => Task.Run(() => library.Songs.Count())).ToArray();
        await Task.WhenAll(readers);

        // One rescan of every file, not one per reader that queued behind the first.
        Assert.Equal(FileCount, parser.ParseCount);
        Assert.False(library.IsDirty);
    }

    [Fact]
    public async Task Songs_ReadDuringRescan_SeesCompleteLibrary()
    {
        var parser = new SlowTagParser(TimeSpan.FromMilliseconds(5));
        using var library = createLibrary(parser);

        library.MarkDirty("test");
        var rescan = Task.Run(() => library.Songs.Count());
        var counts = new List<int>();
        while (!rescan.IsCompleted)
        {
            counts.Add(library.Songs.Count(s => s is not null));
            await Task.Delay(1);
        }

        await rescan;
        Assert.All(counts, count => Assert.Equal(FileCount, count));
    }

    [Fact]
    public void Songs_MarkedDirtyDuringRescan_RescansAgainOnNextRead()
    {
        var parser = new SlowTagParser(TimeSpan.Zero);
        using var library = createLibrary(parser);
        parser.Reset();

        // A change that lands mid-rescan must not be lost when that rescan finishes.
        parser.OnParse = count =>
        {
            if (count == 1)
            {
                library.MarkDirty("change during rescan");
            }
        };
        library.MarkDirty("test");
        _ = library.Songs.Count();
        Assert.True(library.IsDirty);

        parser.OnParse = null;
        _ = library.Songs.Count();
        Assert.False(library.IsDirty);
        Assert.Equal(FileCount * 2, parser.ParseCount);
    }

    [Fact]
    public void SyncHistory_RecordsReasonAndCounts()
    {
        var parser = new SlowTagParser(TimeSpan.Zero) { FailPath = "/music/song-3.mp3" };
        using var library = createLibrary(parser);

        library.MarkDirty("youtube-cached");
        _ = library.Songs.Count();

        var last = library.SyncHistory[^1];
        Assert.Equal("youtube-cached", last.Reason);
        Assert.Equal(FileCount, last.Files);
        Assert.Equal(1, last.Failures);
        Assert.Equal("startup", library.SyncHistory[0].Reason);
    }

    private static Mp3Library createLibrary(ITagParser parser) =>
        new(new FakeFileSource(), parser, new OnDiskDataStore(), NullLogger<Mp3Library>.Instance);

    private sealed class FakeFileSource : IFileSource
    {
        // A folder that doesn't exist, so the library skips its filesystem watcher.
        public string RootFolder => Path.Combine(Path.GetTempPath(), "homespeaker-missing-" + Guid.NewGuid().ToString("N"));

        public IEnumerable<string> GetAllMp3s() => Enumerable.Range(0, FileCount).Select(i => $"/music/song-{i}.mp3").ToList();

        public void SoftDelete(string path) => throw new NotSupportedException();
    }

    private sealed class SlowTagParser(TimeSpan delay) : ITagParser
    {
        private int parseCount;

        public int ParseCount => Volatile.Read(ref parseCount);

        public string? FailPath { get; init; }

        public Action<int>? OnParse { get; set; }

        public void Reset() => Interlocked.Exchange(ref parseCount, 0);

        public Song CreateSong(string fullPath)
        {
            var count = Interlocked.Increment(ref parseCount);
            OnParse?.Invoke(count);
            if (delay > TimeSpan.Zero)
            {
                Thread.Sleep(delay);
            }

            if (fullPath == FailPath)
            {
                throw new InvalidOperationException("Unable to find MP3 tags for " + fullPath);
            }

            return new Song { Name = Path.GetFileNameWithoutExtension(fullPath), Path = fullPath };
        }

        public void UpdateSongTags(string fullPath, string name, string artist, string album) => throw new NotSupportedException();
    }
}
