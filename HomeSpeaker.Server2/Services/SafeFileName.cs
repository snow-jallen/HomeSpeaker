namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Turns an untrusted title (e.g. a YouTube video title) into a file name. Strips characters that
/// are invalid on Windows as well as Linux, since Linux alone allows quotes and control characters.
/// </summary>
public static class SafeFileName
{
    private static readonly char[] unsafeChars = ['"', '/', '\\', ':', '*', '?', '<', '>', '|'];

    public static string From(string title)
    {
        var chars = title.Select(c => char.IsControl(c) || unsafeChars.Contains(c) ? '_' : c).ToArray();
        var name = new string(chars).Trim();
        return name.Trim('.').Length == 0 ? "_" + name : name;
    }
}
