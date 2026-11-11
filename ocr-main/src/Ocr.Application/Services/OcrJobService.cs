using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ocr.Application.Ports;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Application.Services;

public class OcrJobService
{
    private const int MaxErrorLength = 500;

    private readonly IOcrJobRepository _repository;
    private readonly IOcrProcessingEngine _engine;
    private readonly IOcrJobQueue _queue;
    private readonly ILogger<OcrJobService> _logger;
    private readonly IOcrCompletionNotifier? _completionNotifier;
    private readonly string _storageDir;
    private readonly TimeSpan _staleJobTimeout;

    public OcrJobService(
        IOcrJobRepository repository,
        IOcrProcessingEngine engine,
        ILogger<OcrJobService> logger,
        IOcrJobQueue queue,
        string? storageBasePath = null,
        TimeSpan? staleJobTimeout = null,
        IOcrCompletionNotifier? completionNotifier = null)
    {
        _repository = repository;
        _engine = engine;
        _logger = logger;
        _queue = queue;
        _completionNotifier = completionNotifier;
        _storageDir = !string.IsNullOrWhiteSpace(storageBasePath)
            ? storageBasePath
            : Path.Combine(Path.GetTempPath(), "ocr_service_storage");
        _staleJobTimeout = staleJobTimeout ?? TimeSpan.FromMinutes(30);
        Directory.CreateDirectory(_storageDir);
    }

    public async Task<OcrJob> CreateJobAsync(
        byte[] fileBytes,
        string fileName,
        string contentType = "application/pdf",
        string clientReference = "",
        string language = "eng",
        string mode = "skip",
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = $"{jobId}.pdf";
        }

        var storagePath = Path.Combine(_storageDir, $"{jobId}_{safeFileName}");
        await File.WriteAllBytesAsync(storagePath, fileBytes, cancellationToken);

        var job = new OcrJob
        {
            JobId = jobId,
            ClientReference = clientReference,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/pdf" : contentType,
            SizeBytes = fileBytes.Length,
            FileStoragePath = storagePath,
            Language = string.IsNullOrWhiteSpace(language) ? "eng" : language,
            Mode = string.IsNullOrWhiteSpace(mode) ? "skip" : mode,
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.CreateAsync(job, cancellationToken);
        _logger.LogInformation("Job {JobId} created in state Pending for document {FileName}.", job.JobId, job.FileName);

        // Dispatching is a deployment concern: the queue may deliver to an
        // in-process worker (embedded) or intentionally do nothing so a
        // separate Ocr.Worker picks the job up from the database (external).
        await _queue.EnqueueAsync(job.JobId, cancellationToken);

        return job;
    }

    public async Task<OcrJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(jobId, cancellationToken);
    }

    public async Task<(IReadOnlyList<OcrJob> Items, int TotalCount)> GetAllJobsAsync(
        int page = 1,
        int pageSize = 20,
        JobStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(page, pageSize, status, cancellationToken);
    }

    public async Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(jobId, cancellationToken);
        if (job == null)
        {
            return false;
        }

        // Delete stored files from disk
        if (!string.IsNullOrEmpty(job.FileStoragePath) && File.Exists(job.FileStoragePath))
        {
            File.Delete(job.FileStoragePath);
        }

        if (job.Result != null)
        {
            if (!string.IsNullOrEmpty(job.Result.ArchivedPdfPath) && File.Exists(job.Result.ArchivedPdfPath))
            {
                File.Delete(job.Result.ArchivedPdfPath);
            }

            if (!string.IsNullOrEmpty(job.Result.ThumbnailPath) && File.Exists(job.Result.ThumbnailPath))
            {
                File.Delete(job.Result.ThumbnailPath);
            }
        }

        return await _repository.DeleteAsync(jobId, cancellationToken);
    }

    public async Task<Stream?> GetOriginalStreamAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(jobId, cancellationToken);
        if (job == null || string.IsNullOrEmpty(job.FileStoragePath) || !File.Exists(job.FileStoragePath))
        {
            return null;
        }

        return new FileStream(job.FileStoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Claims one specific job and processes it. Returns false when another
    /// worker already claimed it or it is already finished.
    /// </summary>
    public async Task<bool> TryProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.TryClaimJobAsync(jobId, GetStaleThreshold(), cancellationToken);
        if (job is null)
        {
            _logger.LogDebug("Job {JobId} was already claimed or is finished; skipping.", jobId);
            return false;
        }

        await ProcessClaimedJobAsync(job, cancellationToken);
        return true;
    }

    /// <summary>
    /// Claims the oldest available job and processes it. Returns false when
    /// there is nothing pending.
    /// </summary>
    public async Task<bool> ProcessNextJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await _repository.TryClaimNextJobAsync(GetStaleThreshold(), cancellationToken);
        if (job is null)
        {
            return false;
        }

        await ProcessClaimedJobAsync(job, cancellationToken);
        return true;
    }

    /// <summary>Returns abandoned Processing jobs to Pending.</summary>
    public Task<int> ResetStaleJobsAsync(CancellationToken cancellationToken = default) =>
        _repository.ResetStaleProcessingJobsAsync(GetStaleThreshold(), cancellationToken);

    private DateTimeOffset GetStaleThreshold() => DateTimeOffset.UtcNow - _staleJobTimeout;

    private async Task ProcessClaimedJobAsync(OcrJob job, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrEmpty(job.FileStoragePath) || !File.Exists(job.FileStoragePath))
            {
                throw new FileNotFoundException(
                    $"Storage file for job {job.JobId} not found on disk.",
                    job.FileStoragePath);
            }

            var fileBytes = await File.ReadAllBytesAsync(job.FileStoragePath, cancellationToken);
            var options = new OcrProcessingOptions(
                Language: job.Language,
                Mode: job.Mode,
                OutputDirectory: _storageDir,
                JobId: job.JobId);

            var result = await _engine.ProcessAsync(fileBytes, job.FileName, options, cancellationToken);

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Result = result with { DurationMs = stopwatch.ElapsedMilliseconds };
            _logger.LogInformation("Job {JobId} successfully completed in {DurationMs}ms.", job.JobId, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown: return the job to the queue immediately instead
            // of waiting for the stale-job timeout.
            job.Status = JobStatus.Pending;
            job.ProcessingStartedAt = null;
            await _repository.UpdateAsync(job, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for job {JobId}.", job.JobId);
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Error = new OcrJobError(
                Code: ex is TimeoutException ? "PROCESSING_TIMEOUT" : "ENGINE_ERROR",
                Message: Truncate(ex.Message, MaxErrorLength));
        }

        await _repository.UpdateAsync(job, CancellationToken.None);

        if (job.Status == JobStatus.Completed && _completionNotifier is not null)
        {
            // The job identifier is stable and doubles as the correlation ID
            // without adding another persistence field for this Midterm flow.
            await _completionNotifier.NotifyCompletedAsync(
                job.JobId,
                job.JobId.ToString("D"),
                CancellationToken.None);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
