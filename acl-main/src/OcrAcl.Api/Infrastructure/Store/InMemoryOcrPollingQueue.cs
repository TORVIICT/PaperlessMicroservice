namespace OcrAcl.Infrastructure.Store;

using OcrAcl.Application.Ports;
using System.Collections.Concurrent;

/// <summary>
/// In-memory implementation of <see cref="IOcrPollingQueue"/>.
/// Per D-34: ACL-owned in-memory store; state is lost on restart.
/// </summary>
public sealed class InMemoryOcrPollingQueue : IOcrPollingQueue
{
    private readonly ConcurrentQueue<(Guid JobId, string ClientReference, Guid DocumentId)> _queue = new();

    public Task EnqueueAsync(Guid jobId, string clientReference, Guid documentId, CancellationToken cancellationToken = default)
    {
        _queue.Enqueue((jobId, clientReference, documentId));
        return Task.CompletedTask;
    }

    public Task<(Guid JobId, string ClientReference, Guid DocumentId)?> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.TryDequeue(out var item))
        {
            return Task.FromResult<(Guid JobId, string ClientReference, Guid DocumentId)?>(item);
        }
        return Task.FromResult<(Guid JobId, string ClientReference, Guid DocumentId)?>(null);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_queue.Count);
    }
}