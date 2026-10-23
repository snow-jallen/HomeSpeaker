using System.Security.Cryptography;

namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// The optional admin PIN: 4 to 12 digits, stored in <c>Admin:PinHash</c> as
/// <c>pbkdf2-sha256$100000$base64(salt)$base64(hash)</c>. With no hash set, admin pages are open.
/// </summary>
public static class AdminPin
{
    public const string ConfigKey = "Admin:PinHash";
    private const string Scheme = "pbkdf2-sha256";
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static bool IsValidFormat(string? pin) =>
        pin is { Length: >= 4 and <= 12 } && pin.All(char.IsAsciiDigit);

    public static string Hash(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string pin, string? storedHash)
    {
        if (pin is null || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Scheme, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (expected.Length == 0)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static bool IsSet(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[ConfigKey]);
    }

    /// <summary>Only a local absolute path ("/x") is allowed; anything else, including "//host" and "/\host", becomes "/".</summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        // Browsers strip tab/CR/LF inside a URL, so "/	/evil.com" would become "//evil.com".
        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/' || returnUrl.Any(char.IsControl))
        {
            return "/";
        }

        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return "/";
        }

        return returnUrl;
    }
}
