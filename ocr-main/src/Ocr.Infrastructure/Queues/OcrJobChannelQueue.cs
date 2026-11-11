using System.Threading.Channels;
using Ocr.Application.Ports;

namespace Ocr.Infrastructure.Queues;

public sealed class OcrJobChannelQueue : IOcrJobQueue
{
    private readonly Channel<Guid> _channel;

    public OcrJobChannelQueue()
    {
        // Unbounded channel with SingleWriter = false, SingleReader = false
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<Guid>(options);
    }

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
