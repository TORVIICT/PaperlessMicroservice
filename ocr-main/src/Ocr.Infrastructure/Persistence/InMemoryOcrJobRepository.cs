using System.Collections.Concurrent;
using Ocr.Application.Ports;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Infrastructure.Persistence;

public sealed class InMemoryOcrJobRepository : IOcrJobRepository
{
    private readonly ConcurrentDictionary<Guid, OcrJob> _jobs = new();
    private readonly object _gate = new();

    public Task<OcrJob> CreateAsync(OcrJob job, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _jobs[job.JobId] = job;
        }

        return Task.FromResult(job);
    }

    public Task<OcrJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<OcrJob?> TryClaimJobAsync(
        Guid jobId,
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || !IsClaimable(job, staleBefore))
            {
                return Task.FromResult<OcrJob?>(null);
            }

            MarkProcessing(job);
            return Task.FromResult<OcrJob?>(job);
        }
    }

    public Task<OcrJob?> TryClaimNextJobAsync(
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var job = _jobs.Values
                .Where(j => IsClaimable(j, staleBefore))
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefault();

            if (job is null)
            {
                return Task.FromResult<OcrJob?>(null);
            }

            MarkProcessing(job);
            return Task.FromResult<OcrJob?>(job);
        }
    }

    public Task<int> ResetStaleProcessingJobsAsync(
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var staleJobs = _jobs.Values
                .Where(j =>
                    j.Status == JobStatus.Processing &&
                    j.ProcessingStartedAt != null &&
                    j.ProcessingStartedAt < staleBefore)
                .ToList();

            foreach (var job in staleJobs)
            {
                job.Status = JobStatus.Pending;
                job.ProcessingStartedAt = null;
            }

            return Task.FromResult(staleJobs.Count);
        }
    }

    public Task<(IReadOnlyList<OcrJob> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 20, JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filtered = _jobs.Values
            .Where(j => status == null || j.Status == status.Value)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();

        var totalCount = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<OcrJob>, int)>((items, totalCount));
    }

    public Task UpdateAsync(OcrJob job, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _jobs[job.JobId] = job;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var removed = _jobs.TryRemove(jobId, out _);
        return Task.FromResult(removed);
    }

    private static bool IsClaimable(OcrJob job, DateTimeOffset staleBefore) =>
        job.Status == JobStatus.Pending ||
        (job.Status == JobStatus.Processing &&
         job.ProcessingStartedAt != null &&
         job.ProcessingStartedAt < staleBefore);

    private static void MarkProcessing(OcrJob job)
    {
        job.Status = JobStatus.Processing;
        job.ProcessingStartedAt = DateTimeOffset.UtcNow;
    }
}
