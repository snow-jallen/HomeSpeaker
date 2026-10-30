using HomeSpeaker.Server2.Tests.Photos;
using TagLib;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

/// <summary>Writes tiny real MP3s: a few silent MPEG-1 Layer III frames, tagged with TagLib.</summary>
internal static class TestMp3
{
    // MPEG-1 Layer III, no CRC, 128 kbps, 44.1 kHz, no padding, joint stereo: 144 * 128000 / 44100 = 417 bytes per frame.
    private const int FrameLength = 417;
    private static readonly byte[] frameHeader = [0xFF, 0xFB, 0x90, 0x64];

    public static byte[] SilentFrames(int count = 8)
    {
        var bytes = new byte[FrameLength * count];
        for (var i = 0; i < count; i++)
        {
            frameHeader.CopyTo(bytes, i * FrameLength);
        }

        return bytes;
    }

    public static string Create(string folder, string fileName, string title, string? artist, string? album, byte[]? picture = null)
    {
        var path = System.IO.Path.Combine(folder, fileName);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, SilentFrames());
        using var file = TagLib.File.Create(path);
        file.Tag.Title = title;
        file.Tag.Performers = artist is null ? [] : [artist];
        file.Tag.Album = album;
        if (picture is not null)
        {
            file.Tag.Pictures = [new Picture(new ByteVector(picture)) { Type = PictureType.FrontCover, MimeType = "image/jpeg" }];
        }

        file.Save();
        return path;
    }

    public static IPicture[] Pictures(string path)
    {
        using var file = TagLib.File.Create(path);
        return file.Tag.Pictures;
    }

    public static byte[] Jpeg => TestJpeg.Header(600, 600);
}
