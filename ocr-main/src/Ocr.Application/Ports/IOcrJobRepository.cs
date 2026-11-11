using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Application.Ports;

public interface IOcrJobRepository
{
    Task<OcrJob> CreateAsync(OcrJob job, CancellationToken cancellationToken = default);
    Task<OcrJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks a Pending job as Processing. A job left in Processing
    /// past <paramref name="staleBefore"/> is considered abandoned and may be
    /// claimed again.
    /// </summary>
    Task<OcrJob?> TryClaimJobAsync(Guid jobId, DateTimeOffset staleBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the oldest available job and marks it as Processing.
    /// Returns null when there is nothing to process.
    /// </summary>
    Task<OcrJob?> TryClaimNextJobAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken = default);

    /// <summary>Returns abandoned Processing jobs to Pending.</summary>
    Task<int> ResetStaleProcessingJobsAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<OcrJob> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 20, JobStatus? status = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(OcrJob job, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
