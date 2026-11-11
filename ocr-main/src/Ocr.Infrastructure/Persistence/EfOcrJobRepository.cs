using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Ocr.Application.Ports;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Infrastructure.Persistence;

public sealed class EfOcrJobRepository : IOcrJobRepository
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    // The InMemory provider has no row-level locking. A process-wide gate keeps
    // claims deterministic in tests and local fallback mode.
    private static readonly SemaphoreSlim InMemoryGate = new(1, 1);

    private readonly OcrDbContext _dbContext;

    public EfOcrJobRepository(OcrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private bool UsesNpgsql =>
        string.Equals(_dbContext.Database.ProviderName, NpgsqlProvider, StringComparison.Ordinal);

    public async Task<OcrJob> CreateAsync(OcrJob job, CancellationToken cancellationToken = default)
    {
        await _dbContext.Jobs.AddAsync(job, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<OcrJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
    }

    public async Task<OcrJob?> TryClaimJobAsync(
        Guid jobId,
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        if (UsesNpgsql)
        {
            var claimedId = await ExecuteClaimAsync(
                """
                UPDATE ocr_jobs
                SET status = @processing, processing_started_at = @now
                WHERE job_id = @jobId
                  AND (
                    status = @pending
                    OR (status = @processing AND processing_started_at < @staleBefore)
                  )
                RETURNING job_id;
                """,
                command =>
                {
                    AddParameter(command, "jobId", jobId);
                    AddParameter(command, "now", DateTimeOffset.UtcNow);
                    AddParameter(command, "staleBefore", staleBefore);
                    AddParameter(command, "pending", nameof(JobStatus.Pending));
                    AddParameter(command, "processing", nameof(JobStatus.Processing));
                },
                cancellationToken);

            return claimedId.HasValue ? await GetByIdAsync(claimedId.Value, cancellationToken) : null;
        }

        await InMemoryGate.WaitAsync(cancellationToken);
        try
        {
            var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
            if (job is null || !IsClaimable(job, staleBefore))
            {
                return null;
            }

            MarkProcessing(job);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return job;
        }
        finally
        {
            InMemoryGate.Release();
        }
    }

    public async Task<OcrJob?> TryClaimNextJobAsync(
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        if (UsesNpgsql)
        {
            var claimedId = await ExecuteClaimAsync(
                """
                UPDATE ocr_jobs
                SET status = @processing, processing_started_at = @now
                WHERE job_id = (
                    SELECT job_id
                    FROM ocr_jobs
                    WHERE status = @pending
                       OR (status = @processing AND processing_started_at < @staleBefore)
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING job_id;
                """,
                command =>
                {
                    AddParameter(command, "now", DateTimeOffset.UtcNow);
                    AddParameter(command, "staleBefore", staleBefore);
                    AddParameter(command, "pending", nameof(JobStatus.Pending));
                    AddParameter(command, "processing", nameof(JobStatus.Processing));
                },
                cancellationToken);

            return claimedId.HasValue ? await GetByIdAsync(claimedId.Value, cancellationToken) : null;
        }

        await InMemoryGate.WaitAsync(cancellationToken);
        try
        {
            var job = await _dbContext.Jobs
                .Where(j =>
                    j.Status == JobStatus.Pending ||
                    (j.Status == JobStatus.Processing &&
                     j.ProcessingStartedAt != null &&
                     j.ProcessingStartedAt < staleBefore))
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return null;
            }

            MarkProcessing(job);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return job;
        }
        finally
        {
            InMemoryGate.Release();
        }
    }

    public async Task<int> ResetStaleProcessingJobsAsync(
        DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        if (UsesNpgsql)
        {
            return await _dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE ocr_jobs
                SET status = @pending, processing_started_at = NULL
                WHERE status = @processing
                  AND processing_started_at IS NOT NULL
                  AND processing_started_at < @staleBefore;
                """,
                new object[]
                {
                    new Npgsql.NpgsqlParameter("pending", nameof(JobStatus.Pending)),
                    new Npgsql.NpgsqlParameter("processing", nameof(JobStatus.Processing)),
                    new Npgsql.NpgsqlParameter("staleBefore", staleBefore),
                },
                cancellationToken);
        }

        await InMemoryGate.WaitAsync(cancellationToken);
        try
        {
            var staleJobs = await _dbContext.Jobs
                .Where(j =>
                    j.Status == JobStatus.Processing &&
                    j.ProcessingStartedAt != null &&
                    j.ProcessingStartedAt < staleBefore)
                .ToListAsync(cancellationToken);

            foreach (var job in staleJobs)
            {
                job.Status = JobStatus.Pending;
                job.ProcessingStartedAt = null;
            }

            if (staleJobs.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return staleJobs.Count;
        }
        finally
        {
            InMemoryGate.Release();
        }
    }

    public async Task<(IReadOnlyList<OcrJob> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 20, JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Jobs.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task UpdateAsync(OcrJob job, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Jobs.FindAsync([job.JobId], cancellationToken);
        if (existing != null)
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(job);
            existing.Status = job.Status;
            existing.ProcessingStartedAt = job.ProcessingStartedAt;
            existing.CompletedAt = job.CompletedAt;
            existing.FileStoragePath = job.FileStoragePath;
            existing.Result = job.Result;
            existing.Error = job.Error;
        }
        else
        {
            _dbContext.Jobs.Update(job);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([jobId], cancellationToken);
        if (job == null)
        {
            return false;
        }

        _dbContext.Jobs.Remove(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<Guid?> ExecuteClaimAsync(
        string sql,
        Action<DbCommand> configureParameters,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;

        if (mustClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            configureParameters(command);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is Guid claimedId ? claimedId : null;
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
