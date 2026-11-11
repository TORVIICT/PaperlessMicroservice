namespace OcrAcl.Infrastructure.Ocr;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;

/// <summary>
/// Background service that polls OCR jobs until they reach a terminal state.
/// Implements the asynchronous follow-up per D-40: POST returns 202 immediately,
/// and the ACL follows up in the background using the PollingPolicy.
/// </summary>
public sealed class OcrPollingBackgroundService : BackgroundService
{
    private readonly IOcrPollingQueue _pollingQueue;
    private readonly IOcrClient _ocrClient;
    private readonly IDocumentStore _documentStore;
    private readonly PollingPolicy _pollingPolicy;
    private readonly IClock _clock;
    private readonly ILogger<OcrPollingBackgroundService> _logger;

    public OcrPollingBackgroundService(
        IOcrPollingQueue pollingQueue,
        IOcrClient ocrClient,
        IDocumentStore documentStore,
        PollingPolicy pollingPolicy,
        IClock clock,
        ILogger<OcrPollingBackgroundService> logger)
    {
        _pollingQueue = pollingQueue;
        _ocrClient = ocrClient;
        _documentStore = documentStore;
        _pollingPolicy = pollingPolicy;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR polling background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _pollingQueue.TryDequeueAsync(stoppingToken).ConfigureAwait(false);
                
                if (job.HasValue)
                {
                    await PollJobAsync(job.Value.JobId, job.Value.ClientReference, job.Value.DocumentId, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    // No jobs to poll, wait a bit before checking again
                    // Use IClock for testability
                    await _clock.DelayAsync(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OCR polling background service");
                // Wait a bit before retrying to avoid tight loop on persistent errors
                await _clock.DelayAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("OCR polling background service stopped");
    }

    private async Task PollJobAsync(Guid jobId, string clientReference, Guid documentId, CancellationToken stoppingToken)
    {
        var linkedToken = _pollingPolicy.CreateGlobalTimeoutToken(stoppingToken);
        var attempt = 0;
        int transientRetries = 0;

        try
        {
            // Initial wait before first poll (500 ms per OCR-SRC §4)
            await _pollingPolicy.WaitInitialAsync(linkedToken).ConfigureAwait(false);

            while (!linkedToken.IsCancellationRequested)
            {
                attempt++;
                
                OcrJob job;
                try
                {
                    // We need to get the polling URL from the document store
                    var record = await _documentStore.GetByDocumentIdAsync(documentId, linkedToken).ConfigureAwait(false);
                    if (record is null)
                    {
                        _logger.LogWarning("Document {DocumentId} not found for job {JobId}", documentId, jobId);
                        return;
                    }

                    // Poll using the stored polling URL
                    job = await _ocrClient.GetJobByUrlAsync(record.PollingUrl, linkedToken).ConfigureAwait(false);
                    transientRetries = 0; // Reset on success
                }
                catch (HttpRequestException ex) when (IsTransientHttpError(ex))
                {
                    transientRetries++;
                    if (transientRetries > PollingPolicy.MaxTransientRetries)
                    {
                        _logger.LogError(ex, "Max transient retries exceeded for job {JobId}", jobId);
                        await HandlePollingFailureAsync(documentId, "SERVICE_UNAVAILABLE", "OCR service unavailable after retries", linkedToken).ConfigureAwait(false);
                        return;
                    }

                    _logger.LogWarning(ex, "Transient error polling job {JobId}, retry {Retry}/{MaxRetries}", jobId, transientRetries, PollingPolicy.MaxTransientRetries);
                    await _pollingPolicy.WaitTransientRetryAsync(linkedToken).ConfigureAwait(false);
                    continue;
                }
                catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Polling cancelled for job {JobId}", jobId);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error polling job {JobId}", jobId);
                    await HandlePollingFailureAsync(documentId, "OCR_PROCESSING_FAILED", $"Polling error: {ex.Message}", linkedToken).ConfigureAwait(false);
                    return;
                }

                // Check job status
                if (job.Status == OcrStatus.Completed)
                {
                    await HandleCompletedAsync(documentId, job, linkedToken).ConfigureAwait(false);
                    return;
                }

                if (job.Status == OcrStatus.Failed)
                {
                    await HandleFailedAsync(documentId, job, linkedToken).ConfigureAwait(false);
                    return;
                }

                // Still processing - wait for next poll
                await _pollingPolicy.WaitPollDelayAsync(attempt, linkedToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Service stopping, re-enqueue the job for next startup
            _logger.LogInformation("Service stopping, re-enqueueing job {JobId}", jobId);
            await _pollingQueue.EnqueueAsync(jobId, clientReference, documentId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
        {
            // Global timeout reached
            _logger.LogWarning("Global timeout reached for job {JobId}", jobId);
            await HandlePollingFailureAsync(documentId, "PROCESSING_TIMEOUT", "OCR processing timed out", stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task HandleCompletedAsync(Guid documentId, OcrJob job, CancellationToken cancellationToken)
    {
        if (job.Result is null)
        {
            _logger.LogError("Job {JobId} completed but result is null", job.JobId);
            await HandlePollingFailureAsync(documentId, "ENGINE_ERROR", "OCR completed without result", cancellationToken).ConfigureAwait(false);
            return;
        }

        var completedAt = _clock.UtcNow;
        await _documentStore.UpdateResultAsync(documentId, job.Result, completedAt, cancellationToken).ConfigureAwait(false);
        
        _logger.LogInformation("Job {JobId} completed successfully for document {DocumentId}", job.JobId, documentId);
        
        // TODO: Trigger monolith delivery when OQ-01 is answered (T-072)
    }

    private async Task HandleFailedAsync(Guid documentId, OcrJob job, CancellationToken cancellationToken)
    {
        var completedAt = _clock.UtcNow;
        
        if (job.Error is not null)
        {
            await _documentStore.UpdateErrorAsync(documentId, job.Error, completedAt, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Job {JobId} failed for document {DocumentId}: {ErrorCode} - {ErrorMessage}", 
                job.JobId, documentId, job.Error.Code, job.Error.Message);
        }
        else
        {
            var error = new OcrJobError { Code = "ENGINE_ERROR", Message = "OCR job failed without error details" };
            await _documentStore.UpdateErrorAsync(documentId, error, completedAt, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Job {JobId} failed for document {DocumentId} without error details", job.JobId, documentId);
        }
        
        // TODO: Handle retry-after-failed when OQ-07 is answered (T-066)
    }

    private async Task HandlePollingFailureAsync(Guid documentId, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        var completedAt = _clock.UtcNow;
        var error = new OcrJobError { Code = errorCode, Message = errorMessage };
        await _documentStore.UpdateErrorAsync(documentId, error, completedAt, cancellationToken).ConfigureAwait(false);
        _logger.LogError("Polling failed for document {DocumentId}: {ErrorCode} - {ErrorMessage}", documentId, errorCode, errorMessage);
    }

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
        // Check for 503 or socket/connection errors
        if (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            return true;
        
        // Check for socket/connection errors (no specific status code)
        return ex.InnerException is System.Net.Sockets.SocketException;
    }
}