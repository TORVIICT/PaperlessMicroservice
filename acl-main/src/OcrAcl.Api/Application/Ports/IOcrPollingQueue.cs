namespace OcrAcl.Application.Ports;

using OcrAcl.Application.Models;

/// <summary>
/// Port for enqueueing and dequeueing OCR jobs that need background polling.
/// This isolates the background orchestration mechanism from the use cases.
/// </summary>
public interface IOcrPollingQueue
{
    /// <summary>
    /// Enqueues a job for background polling.
    /// </summary>
    /// <param name="jobId">The OCR job ID.</param>
    /// <param name="clientReference">The client reference.</param>
    /// <param name="documentId">The ACL document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnqueueAsync(Guid jobId, string clientReference, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to dequeue a job for polling.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job to poll, or null if queue is empty.</returns>
    Task<(Guid JobId, string ClientReference, Guid DocumentId)?> TryDequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending jobs in the queue.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}