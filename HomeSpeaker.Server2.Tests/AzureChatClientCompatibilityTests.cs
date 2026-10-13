using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace HomeSpeaker.Server2.Tests;

/// <summary>
/// Builds the same Azure chat client object graph as Program.cs (AddChatClient) without touching the network.
/// Azure.AI.OpenAI 2.1.0 runs against a newer OpenAI package, so a binary incompatibility would surface
/// here as a MissingMethodException or TypeLoadException.
/// </summary>
public sealed class AzureChatClientCompatibilityTests
{
    private const string CannedCompletion = """
        {
          "id": "chatcmpl-1",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "test-deployment",
          "choices": [
            { "index": 0, "finish_reason": "stop", "message": { "role": "assistant", "content": "hello" } }
          ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CannedCompletion, Encoding.UTF8, "application/json")
            });
        }
    }

    private static IChatClient buildClient(HttpMessageHandler handler)
    {
        var options = new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(45),
            Transport = new HttpClientPipelineTransport(new HttpClient(handler))
        };
        var azureClient = new AzureOpenAIClient(
            new Uri("https://example.openai.azure.com/"),
            new ApiKeyCredential("fake-key"),
            options);
        return azureClient.GetChatClient("test-deployment").AsIChatClient();
    }

    [Fact]
    public void AzureChatClient_BuildsAnIChatClient()
    {
        using var client = buildClient(new StubHandler());

        Assert.NotNull(client);
    }

    [Fact]
    public async Task AzureChatClient_RoundTripsAgainstAStubTransport()
    {
        var handler = new StubHandler();
        using var client = buildClient(handler);

        var response = await client.GetResponseAsync("hi");

        Assert.Equal("hello", response.Text);
        Assert.Contains("example.openai.azure.com", handler.RequestUri?.Host, StringComparison.Ordinal);
    }
}
