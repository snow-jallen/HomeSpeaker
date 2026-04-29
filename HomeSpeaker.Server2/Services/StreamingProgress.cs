using Grpc.Core;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

internal sealed class StreamingProgress : IProgress<double>
{
    private readonly IServerStreamWriter<CacheVideoReply> responseStream;
    private readonly string title;
    private readonly ILogger logger;
    private double lastProgress;

    public StreamingProgress(IServerStreamWriter<CacheVideoReply> responseStream, string title, ILogger logger)
    {
        this.responseStream = responseStream;
        this.title = title;
        this.logger = logger;
    }

    public void Report(double value) => _ = reportAsync(value);

    private async Task reportAsync(double value)
    {
        logger.LogInformation("Progress of {Title} is {Value}", title, value);
        if (value > lastProgress + .01)
        {
            await responseStream.WriteAsync(new CacheVideoReply { PercentComplete = value, Title = title });
            lastProgress = value;
        }
    }
}