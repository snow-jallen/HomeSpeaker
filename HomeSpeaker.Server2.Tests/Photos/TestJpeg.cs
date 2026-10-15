namespace HomeSpeaker.Server2.Tests.Photos;

/// <summary>The header of a JPEG with the given size: SOI, an APP0 segment, then a baseline frame header. No image data.</summary>
internal static class TestJpeg
{
    public static byte[] Header(int width, int height, byte frameMarker = 0xC0) =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x06, 0x4A, 0x46, 0x49, 0x46,
        0xFF, frameMarker, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xD9,
    ];
}
