namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>A downloaded cover, already checked to be a JPEG or PNG by its leading bytes.</summary>
public sealed record AlbumArtImage(byte[] Bytes, string MimeType)
{
    private static readonly byte[] jpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>"image/jpeg" or "image/png" from the file signature, or null for anything else.</summary>
    public static string? DetectMimeType(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(jpegMagic) ? "image/jpeg"
        : bytes.StartsWith(pngMagic) ? "image/png"
        : null;
}
