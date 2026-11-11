using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ocr.Application.Services;
using Ocr.Infrastructure.Workers;

namespace Ocr.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly WorkerSettings _settings;
    private DateTimeOffset _lastStaleReset = DateTimeOffset.MinValue;

    public Worker(
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger,
        WorkerSettings settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Dedicated OCR worker started at {Time}. Polling every {PollingInterval}; stale timeout {StaleJobTimeout}.",
            DateTimeOffset.UtcNow,
            _settings.PollingInterval,
            _settings.StaleJobTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<OcrJobService>();

                if (ShouldResetStaleJobs())
                {
                    await ResetStaleJobsAsync(jobService, stoppingToken);
                }

                var processed = await jobService.ProcessNextJobAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(_settings.PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while claiming or processing OCR jobs.");
                await Task.Delay(_settings.PollingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Dedicated OCR worker stopped gracefully at {Time}.", DateTimeOffset.UtcNow);
    }

    private bool ShouldResetStaleJobs() =>
        DateTimeOffset.UtcNow - _lastStaleReset >= _settings.StaleCheckInterval;

    private async Task ResetStaleJobsAsync(OcrJobService jobService, CancellationToken cancellationToken)
    {
        _lastStaleReset = DateTimeOffset.UtcNow;
        var recovered = await jobService.ResetStaleJobsAsync(cancellationToken);

        if (recovered > 0)
        {
            _logger.LogWarning(
                "Recovered {Count} OCR jobs left in Processing by a previous run.",
                recovered);
        }
    }
}
