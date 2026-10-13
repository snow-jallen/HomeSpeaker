namespace HomeSpeaker.Server2.Services.Photos;

public static class ImmichOptionsFallback
{
    public const string ApiKeyConfigKey = "IMMICH_API_KEY";

    public static void Apply(ImmichOptions options, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ApiKey = configuration[ApiKeyConfigKey];
        }
    }
}
