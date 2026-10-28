using Microsoft.AspNetCore.Http;

namespace HomeSpeaker.Server2.Tests;

public sealed class KioskTests
{
    private static DefaultHttpContext context(string host, string? query = null, string? cookie = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        if (query is not null)
        {
            ctx.Request.QueryString = new QueryString(query);
        }

        if (cookie is not null)
        {
            ctx.Request.Headers.Cookie = cookie;
        }

        return ctx;
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("[::1]", true)]
    [InlineData("kitchen.boston-cloud.ts.net", false)]
    [InlineData("192.168.1.50", false)]
    public void Host_DecidesWhenNoOverride(string host, bool expected) =>
        Assert.Equal(expected, Kiosk.IsKiosk(context(host).Request));

    [Fact]
    public void CookieOne_MakesRemoteHostKiosk() =>
        Assert.True(Kiosk.IsKiosk(context("kitchen.boston-cloud.ts.net", cookie: "hs-kiosk=1").Request));

    [Fact]
    public void CookieZero_OverridesLocalhost() =>
        Assert.False(Kiosk.IsKiosk(context("localhost", cookie: "hs-kiosk=0").Request));

    [Fact]
    public void Query_OverridesCookieAndHost()
    {
        Assert.True(Kiosk.IsKiosk(context("phone.ts.net", "?kiosk=1", "hs-kiosk=0").Request));
        Assert.False(Kiosk.IsKiosk(context("localhost", "?kiosk=0").Request));
    }

    [Theory]
    [InlineData("phone.ts.net", null, null, true)]
    [InlineData("localhost", null, null, true)]
    [InlineData("phone.ts.net", "?kiosk=1", null, true)]
    [InlineData("phone.ts.net", null, "hs-kiosk=0", false)]
    [InlineData("localhost", "?kiosk=0", null, false)]
    public void ShowsAmbient_EveryBrowserUnlessOptedOut(string host, string? query, string? cookie, bool expected) =>
        Assert.Equal(expected, Kiosk.ShowsAmbient(context(host, query, cookie).Request));

    [Fact]
    public void ApplyQueryOverride_SetsCookie()
    {
        var ctx = context("localhost", "?kiosk=0");
        Kiosk.ApplyQueryOverride(ctx);
        Assert.Contains("hs-kiosk=0", ctx.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyQueryOverride_WithoutQuery_DoesNothing()
    {
        var ctx = context("localhost");
        Kiosk.ApplyQueryOverride(ctx);
        Assert.Equal(0, ctx.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void Detect_RemoteHostWithoutCookie_IsAuto() =>
        Assert.Equal(KioskMode.Auto, Kiosk.Detect(context("kitchen.boston-cloud.ts.net").Request));

    [Fact]
    public void Detect_CookieZero_IsOffEvenOnLoopback() =>
        Assert.Equal(KioskMode.Off, Kiosk.Detect(context("localhost", cookie: "hs-kiosk=0").Request));

    [Fact]
    public void Detect_CookieOne_IsOn() =>
        Assert.Equal(KioskMode.On, Kiosk.Detect(context("phone.ts.net", cookie: "hs-kiosk=1").Request));

    [Fact]
    public void Detect_QueryOne_IsOn() =>
        Assert.Equal(KioskMode.On, Kiosk.Detect(context("phone.ts.net", "?kiosk=1", "hs-kiosk=0").Request));

    [Fact]
    public void Detect_QueryZero_IsOff() =>
        Assert.Equal(KioskMode.Off, Kiosk.Detect(context("localhost", "?kiosk=0").Request));

    [Fact]
    public void Detect_Loopback_IsOn() =>
        Assert.Equal(KioskMode.On, Kiosk.Detect(context("127.0.0.1").Request));
}
