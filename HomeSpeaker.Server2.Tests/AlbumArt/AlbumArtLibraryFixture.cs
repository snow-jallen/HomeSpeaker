using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.AlbumArt;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

/// <summary>A real <see cref="Mp3Library"/> over tiny MP3s in a temp folder, plus the finder built on it.</summary>
internal sealed class AlbumArtLibraryFixture : IDisposable
{
    private readonly TempDirectory directory = new();
    private Mp3Library? library;
    private MissingAlbumArtFinder? finder;

    public string Folder => directory.Path;

    public string Add(string fileName, string title, string? artist, string? album, bool withArt = false) =>
        TestMp3.Create(Folder, fileName, title, artist, album, withArt ? TestMp3.Jpeg : null);

    /// <summary>Starts the library (it scans on construction), so add files first.</summary>
    public Mp3Library Library => library ??= new Mp3Library(
        new DefaultFileSource(Folder), new DefaultTagParser(NullLogger<DefaultTagParser>.Instance), new OnDiskDataStore(), NullLogger<Mp3Library>.Instance);

    public MissingAlbumArtFinder Finder => finder ??= new MissingAlbumArtFinder(Library, NullLogger<MissingAlbumArtFinder>.Instance);

    public void Dispose()
    {
        finder?.Dispose();
        library?.Dispose();
        directory.Dispose();
    }
}
