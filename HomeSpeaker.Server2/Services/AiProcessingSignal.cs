using System.Threading.Channels;

namespace HomeSpeaker.Server2.Services;

public sealed class AiProcessingSignal
{
    private readonly Channel<bool> channel = Channel.CreateUnbounded<bool>();

    public ValueTask SignalAsync(CancellationToken cancellationToken) =>
        channel.Writer.WriteAsync(true, cancellationToken);

    public async Task WaitForNextAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(delay, cancellationToken);
        var signalTask = channel.Reader.ReadAsync(cancellationToken).AsTask();
        var completed = await Task.WhenAny(delayTask, signalTask);
        if (completed == signalTask)
        {
            while (channel.Reader.TryRead(out _))
            {
            }
        }
    }
}
