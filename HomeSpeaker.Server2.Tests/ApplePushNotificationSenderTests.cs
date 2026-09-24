using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests;

public sealed class ApplePushNotificationSenderTests
{
    private const string SandboxHost = "api.sandbox.push.apple.com";
    private const string ProductionHost = "api.push.apple.com";
    private const string DeviceToken = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task RetriesProductionWhenSandboxReportsBadDeviceToken()
    {
        var handler = new StubApnsHandler(host => host == ProductionHost
            ? StubResponse.Success()
            : StubResponse.BadRequest("BadDeviceToken"));
        var sender = createSender(handler, useSandbox: true);

        var result = await sender.SendAsync(PushNotificationPlatform.Apns, DeviceToken, "title", "body", alertData());

        Assert.True(result.IsSuccess);
        Assert.False(result.ShouldDeactivateDevice);
        Assert.Equal(new[] { SandboxHost, ProductionHost }, handler.RequestedHosts);
    }

    [Fact]
    public async Task RetriesSandboxWhenProductionReportsBadDeviceToken()
    {
        var handler = new StubApnsHandler(host => host == SandboxHost
            ? StubResponse.Success()
            : StubResponse.BadRequest("BadDeviceToken"));
        var sender = createSender(handler, useSandbox: false);

        var result = await sender.SendAsync(PushNotificationPlatform.Apns, DeviceToken, "title", "body", alertData());

        Assert.True(result.IsSuccess);
        Assert.False(result.ShouldDeactivateDevice);
        Assert.Equal(new[] { ProductionHost, SandboxHost }, handler.RequestedHosts);
    }

    [Fact]
    public async Task DeactivatesWhenBothEnvironmentsReportBadDeviceToken()
    {
        var handler = new StubApnsHandler(_ => StubResponse.BadRequest("BadDeviceToken"));
        var sender = createSender(handler, useSandbox: false);

        var result = await sender.SendAsync(PushNotificationPlatform.Apns, DeviceToken, "title", "body", alertData());

        Assert.False(result.IsSuccess);
        Assert.True(result.ShouldDeactivateDevice);
        Assert.Equal("BadDeviceToken", result.ErrorCode);
        Assert.Equal(new[] { ProductionHost, SandboxHost }, handler.RequestedHosts);
    }

    [Fact]
    public async Task DeactivatesWithoutRetryWhenTokenUnregistered()
    {
        var handler = new StubApnsHandler(_ => StubResponse.Gone("Unregistered"));
        var sender = createSender(handler, useSandbox: true);

        var result = await sender.SendAsync(PushNotificationPlatform.Apns, DeviceToken, "title", "body", alertData());

        Assert.False(result.IsSuccess);
        Assert.True(result.ShouldDeactivateDevice);
        Assert.Equal("Unregistered", result.ErrorCode);
        Assert.Equal(new[] { SandboxHost }, handler.RequestedHosts);
    }

    [Fact]
    public async Task DoesNotRetryOrDeactivateOnTransientError()
    {
        var handler = new StubApnsHandler(_ => StubResponse.ServiceUnavailable());
        var sender = createSender(handler, useSandbox: true);

        var result = await sender.SendAsync(PushNotificationPlatform.Apns, DeviceToken, "title", "body", alertData());

        Assert.False(result.IsSuccess);
        Assert.False(result.ShouldDeactivateDevice);
        Assert.Equal(new[] { SandboxHost }, handler.RequestedHosts);
    }

    private static IReadOnlyDictionary<string, string> alertData() => new Dictionary<string, string>
    {
        ["alertKey"] = "temperature-window-state",
        ["stateKey"] = "keep-closed"
    };

    private static ApplePushNotificationSender createSender(StubApnsHandler handler, bool useSandbox)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportPkcs8PrivateKeyPem();
        var options = Options.Create(new PushNotificationOptions
        {
            Enabled = true,
            Apns = new PushNotificationOptions.ApnsOptions
            {
                TeamId = "ABCDE12345",
                KeyId = "KEY1234567",
                Topic = "com.homespeaker.mobile",
                UseSandbox = useSandbox,
                PrivateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pem))
            }
        });

        return new ApplePushNotificationSender(
            new StubHttpClientFactory(handler),
            options,
            NullLogger<ApplePushNotificationSender>.Instance,
            TimeProvider.System);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => this.handler = handler;

        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubApnsHandler : HttpMessageHandler
    {
        private readonly Func<string, StubResponse> responder;
        private readonly List<string> requestedHosts = new();

        public StubApnsHandler(Func<string, StubResponse> responder) => this.responder = responder;

        public IReadOnlyList<string> RequestedHosts => requestedHosts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            requestedHosts.Add(host);

            var stub = responder(host);
            var response = new HttpResponseMessage(stub.StatusCode);
            if (stub.Body is not null)
            {
                response.Content = new StringContent(stub.Body);
            }

            return Task.FromResult(response);
        }
    }

    private sealed record StubResponse(HttpStatusCode StatusCode, string? Body)
    {
        public static StubResponse Success() => new(HttpStatusCode.OK, null);

        public static StubResponse BadRequest(string reason) => new(HttpStatusCode.BadRequest, $"{{\"reason\":\"{reason}\"}}");

        public static StubResponse Gone(string reason) => new(HttpStatusCode.Gone, $"{{\"reason\":\"{reason}\"}}");

        public static StubResponse ServiceUnavailable() => new(HttpStatusCode.ServiceUnavailable, "{\"reason\":\"ServiceUnavailable\"}");
    }
}
