namespace HomeSpeaker.Server2.Services.Photos;

public static class PhotoSourceSelector
{
    /// <summary><c>Photos:Source</c> (immich | folder, case-insensitive) wins; otherwise Immich when configured, else the folder.</summary>
    public static PhotoSourceKind Resolve(IConfiguration configuration, ImmichOptions immich)
    {
        var value = configuration["Photos:Source"]?.Trim();
        if (string.Equals(value, "immich", StringComparison.OrdinalIgnoreCase))
        {
            return PhotoSourceKind.Immich;
        }

        if (string.Equals(value, "folder", StringComparison.OrdinalIgnoreCase))
        {
            return PhotoSourceKind.Folder;
        }

        return immich.IsConfigured ? PhotoSourceKind.Immich : PhotoSourceKind.Folder;
    }
}
