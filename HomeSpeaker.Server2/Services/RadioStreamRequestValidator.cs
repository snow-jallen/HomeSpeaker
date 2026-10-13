namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Checks the required fields of a radio stream create or update request. System.Text.Json does not
/// enforce required record parameters, so a body that omits name or url binds them as null.
/// </summary>
public static class RadioStreamRequestValidator
{
    /// <summary>Returns an error message, or null when both the name and the URL are present.</summary>
    public static string? Validate(string? name, string? url)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return "Url is required.";
        }

        return null;
    }
}
