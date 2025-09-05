using Grpc.Core;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

internal class StreamingProgress : IProgress<double>
{
    private readonly IServerStreamWriter<CacheVideoReply> _responseStream;
    private readonly string _title;
    private readonly ILogger _logger;
    private double _lastProgress;

    public StreamingProgress(IServerStreamWriter<CacheVideoReply> responseStream, string title, ILogger logger)
    {
        _responseStream = responseStream;
        _title = title;
        _logger = logger;
    }

    public async void Report(double value)
    {
        _logger.LogInformation("Progress of {title} is {value}", _title, value);
        if (value > _lastProgress + .01)
        {
            await _responseStream.WriteAsync(new CacheVideoReply { PercentComplete = value, Title = _title });
            _lastProgress = value;
        }
    }
}