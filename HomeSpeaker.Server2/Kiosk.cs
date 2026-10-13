using System.Net;

namespace HomeSpeaker.Server2;

/// <summary>
/// Decides whether the browser is a wall-mounted kiosk (gets the ambient and night screens).
/// Order of precedence: ?kiosk=1/0 query, then the hs-kiosk cookie, then "is the host loopback".
/// Otherwise the answer is <see cref="KioskMode.Auto"/> and idleController.js decides from the screen size (1024x600).
/// </summary>
public static class Kiosk
{
    public const string CookieName = "hs-kiosk";
    private const string QueryKey = "kiosk";

    public static bool IsKiosk(HttpRequest request) => Detect(request) == KioskMode.On;

    public static KioskMode Detect(HttpRequest request)
    {
        var query = request.Query[QueryKey].ToString();
        if (string.Equals(query, "1", StringComparison.Ordinal))
        {
            return KioskMode.On;
        }

        if (string.Equals(query, "0", StringComparison.Ordinal))
        {
            return KioskMode.Off;
        }

        if (request.Cookies.TryGetValue(CookieName, out var cookie))
        {
            return string.Equals(cookie, "1", StringComparison.Ordinal) ? KioskMode.On : KioskMode.Off;
        }

        return isLoopbackHost(request.Host.Host) ? KioskMode.On : KioskMode.Auto;
    }

    public static void ApplyQueryOverride(HttpContext context)
    {
        var query = context.Request.Query[QueryKey].ToString();
        if (!string.Equals(query, "1", StringComparison.Ordinal) && !string.Equals(query, "0", StringComparison.Ordinal))
        {
            return;
        }

        context.Response.Cookies.Append(CookieName, query, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(10),
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
    }

    private static bool isLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}
