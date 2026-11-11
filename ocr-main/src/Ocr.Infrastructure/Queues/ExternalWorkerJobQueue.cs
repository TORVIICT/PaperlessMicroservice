using Microsoft.Extensions.Logging;
using Ocr.Application.Ports;

namespace Ocr.Infrastructure.Queues;

/// <summary>
/// Queue implementation used when jobs are processed by a separate Ocr.Worker
/// process. Enqueueing is a no-op on purpose: the job stays Pending in the
/// database and the external worker claims it from there.
/// </summary>
public sealed class ExternalWorkerJobQueue : IOcrJobQueue
{
    private readonly ILogger<ExternalWorkerJobQueue> _logger;

    public ExternalWorkerJobQueue(ILogger<ExternalWorkerJobQueue> logger)
    {
        _logger = logger;
    }

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Job {JobId} persisted as Pending for the external OCR worker.",
            jobId);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The external worker reads pending jobs from the database.");
}
