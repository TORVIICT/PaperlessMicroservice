using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ocr.Application.Ports;
using Ocr.Application.Services;

namespace Ocr.Infrastructure.Workers;

public sealed class OcrBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOcrJobQueue _queue;
    private readonly ILogger<OcrBackgroundWorker> _logger;
    private readonly WorkerSettings _settings;

    public OcrBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IOcrJobQueue queue,
        ILogger<OcrBackgroundWorker> logger,
        WorkerSettings settings)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OCR embedded worker started. Stale job timeout: {StaleJobTimeout}.",
            _settings.StaleJobTimeout);

        await ResetStaleJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Worker dequeued job {JobId}. Starting execution...", jobId);

                using var scope = _scopeFactory.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<OcrJobService>();

                await jobService.TryProcessJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OCR Background Worker processing loop.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("OCR Background Worker stopped gracefully.");
    }

    private async Task ResetStaleJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<OcrJobService>();
            var recovered = await jobService.ResetStaleJobsAsync(cancellationToken);

            if (recovered > 0)
            {
                _logger.LogWarning(
                    "Recovered {Count} OCR jobs left in Processing by a previous run.",
                    recovered);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reset stale OCR jobs at startup.");
        }
    }
}
