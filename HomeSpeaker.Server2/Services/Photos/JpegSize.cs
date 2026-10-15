namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>Reads a JPEG's pixel size from its frame header without decoding the image.</summary>
public static class JpegSize
{
    public static bool TryRead(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return false;
        }

        while (true)
        {
            if (stream.ReadByte() != 0xFF)
            {
                return false;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF); // fill bytes

            if (marker is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                continue; // standalone markers carry no length
            }

            if (marker is < 0 or 0xD9 or 0xDA)
            {
                return false; // end of file, end of image or start of scan before any frame header
            }

            var length = readUInt16(stream);
            if (length < 2)
            {
                return false;
            }

            if (isStartOfFrame(marker))
            {
                stream.ReadByte(); // sample precision
                height = readUInt16(stream);
                width = readUInt16(stream);
                return width > 0 && height > 0;
            }

            if (!skip(stream, length - 2))
            {
                return false;
            }
        }
    }

    // SOF0-SOF15, except DHT (C4), JPG (C8) and DAC (CC), which share the range.
    private static bool isStartOfFrame(int marker) => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    private static int readUInt16(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        return high < 0 || low < 0 ? -1 : (high << 8) | low;
    }

    private static bool skip(Stream stream, int count)
    {
        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return stream.Position <= stream.Length;
        }

        for (var i = 0; i < count; i++)
        {
            if (stream.ReadByte() < 0)
            {
                return false;
            }
        }

        return true;
    }
}
