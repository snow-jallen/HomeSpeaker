namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>A cover ready to embed, already checked to be a JPEG or PNG by its leading bytes.</summary>
public sealed record AlbumArtImage(byte[] Bytes, string MimeType)
{
    private static readonly byte[] jpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>"image/jpeg" or "image/png" from the file signature, or null for anything else.</summary>
    public static string? DetectMimeType(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(jpegMagic) ? "image/jpeg"
        : bytes.StartsWith(pngMagic) ? "image/png"
        : null;

    /// <summary>
    /// Reads an uploaded or pasted image, which the review page's script has already redrawn as a JPEG (so a GIF or
    /// WebP arrives as one). Throws <see cref="AlbumArtDownloadException"/> if it is too big or isn't a JPEG or PNG after all.
    /// </summary>
    public static async Task<AlbumArtImage> FromUploadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > AlbumArtDownloader.MaxBytes)
            {
                throw new AlbumArtDownloadException($"That image is over {AlbumArtDownloader.MaxBytes / (1024 * 1024)} MB.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var bytes = buffer.ToArray();
        var mimeType = DetectMimeType(bytes) ?? throw new AlbumArtDownloadException("That file isn't a JPEG or PNG image.");
        return new AlbumArtImage(bytes, mimeType);
    }

    /// <summary>A data: URL, so the review page can preview an upload without serving it.</summary>
    public string ToDataUrl() => $"data:{MimeType};base64,{Convert.ToBase64String(Bytes)}";
}
