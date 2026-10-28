namespace HomeSpeaker.Server2.Tests;

public class AmbientAttributeTests
{
    [Theory]
    [InlineData("/", "data-ambient=\"true\"")]
    [InlineData("/?kiosk=0", "data-ambient=\"false\"")]
    public async Task Body_CarriesAmbientAttribute(string url, string expected)
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(new Uri(url, UriKind.Relative));

        Assert.Contains(expected, html, StringComparison.Ordinal);
    }
}
