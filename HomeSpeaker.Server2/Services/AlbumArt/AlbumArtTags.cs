using TagLib;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Reads and writes the embedded picture that <c>GET /api/homespeaker/songs/{id}/art</c> serves.</summary>
public static class AlbumArtTags
{
    /// <summary>True when the file has any embedded picture. Skips the audio properties and picture data, so it only reads the tags.</summary>
    public static bool HasPicture(string path)
    {
        using var file = TagLib.File.Create(path, ReadStyle.PictureLazy);
        return file.Tag.Pictures is { Length: > 0 };
    }

    /// <summary>Replaces the file's pictures with <paramref name="image"/> as its front cover and saves.</summary>
    public static void EmbedFrontCover(string path, AlbumArtImage image)
    {
        using var file = TagLib.File.Create(path);
        file.Tag.Pictures =
        [
            new Picture(new ByteVector(image.Bytes))
            {
                Type = PictureType.FrontCover,
                MimeType = image.MimeType,
                Description = "Front cover",
            },
        ];
        file.Save();
    }
}
