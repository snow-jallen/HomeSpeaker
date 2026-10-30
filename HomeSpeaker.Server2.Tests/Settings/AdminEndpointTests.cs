using System.Net;
using System.Text.RegularExpressions;
using HomeSpeaker.Server2.Settings;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed partial class AdminEndpointTests
{
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex tokenRegex();

    private static TestServerFactory withPin(string pin = "1234") =>
        new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Admin:PinHash"] = AdminPin.Hash(pin) });

    private static HttpClient noRedirectClient(TestServerFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> antiforgeryToken(HttpClient client)
    {
        var page = await client.GetStringAsync(new Uri("/unlock", UriKind.Relative));
        var match = tokenRegex().Match(page);
        Assert.True(match.Success, "unlock page must render an antiforgery token");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> postUnlock(HttpClient client, string pin, string returnUrl = "/settings")
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["pin"] = pin,
            ["returnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = await antiforgeryToken(client),
        });
        return await client.PostAsync(new Uri("/api/admin/unlock", UriKind.Relative), form);
    }

    private static bool setsAdminCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
        cookies.Any(c => c.StartsWith("hs-admin=", StringComparison.Ordinal));

    [Fact]
    public async Task Unlock_CorrectPin_SetsCookieAndRedirects()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await postUnlock(client, "1234");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/settings", response.Headers.Location?.OriginalString);
        Assert.True(setsAdminCookie(response));

        using var check = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
    }

    [Fact]
    public async Task Unlock_OffSiteReturnUrl_RedirectsToRoot()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await postUnlock(client, "1234", "//evil.com");

        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Unlock_WrongPin_RedirectsWithError()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await postUnlock(client, "0000");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/unlock?error=1&returnUrl=%2Fsettings", response.Headers.Location?.OriginalString);
        Assert.False(setsAdminCookie(response));
    }

    [Fact]
    public async Task Unlock_SixthTry_RedirectsLocked()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        for (var i = 0; i < 5; i++)
        {
            using var wrong = await postUnlock(client, "0000");
        }

        using var response = await postUnlock(client, "1234");

        Assert.Equal("/unlock?error=locked&returnUrl=%2Fsettings", response.Headers.Location?.OriginalString);
        Assert.False(setsAdminCookie(response));
    }

    [Fact]
    public async Task Unlock_WithoutAntiforgeryToken_IsRejected()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["pin"] = "1234", ["returnUrl"] = "/" });

        using var response = await client.PostAsync(new Uri("/api/admin/unlock", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lock_WithoutAntiforgeryToken_IsRejected()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>());

        using var response = await client.PostAsync(new Uri("/api/admin/lock", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnlockPage_IsStaticSsr_NoInteractiveCircuit()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        var html = await client.GetStringAsync(new Uri("/unlock", UriKind.Relative));

        Assert.DoesNotContain("\"type\":\"server\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsPage_WithPinAndNoCookie_RedirectsToUnlock()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? "";
        Assert.Contains("/unlock?ReturnUrl=", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/settings")]
    [InlineData("/setup")]
    [InlineData("/anchors/edit")]
    [InlineData("/autoplay")]
    [InlineData("/album-art")]
    [InlineData("/admin/album-art")]
    public async Task AdminPage_WithPinAndNoCookie_RedirectsToUnlock(string path)
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? "";
        Assert.Contains("/unlock?ReturnUrl=", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/anchors")]
    [InlineData("/aspire")]
    public async Task OpenPage_WithPinAndNoCookie_IsNotRedirected(string path)
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminCheck_NoPinSet_Returns204()
    {
        using var factory = new TestServerFactory();
        using var client = noRedirectClient(factory);

        using var response = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Lock_SignsOutAndRedirectsHome()
    {
        using var factory = withPin();
        using var client = noRedirectClient(factory);
        using var unlock = await postUnlock(client, "1234");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await antiforgeryToken(client) });
        using var lockResponse = await client.PostAsync(new Uri("/api/admin/lock", UriKind.Relative), form);

        Assert.Equal("/", lockResponse.Headers.Location?.OriginalString);
        using var check = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, check.StatusCode);
    }
}
