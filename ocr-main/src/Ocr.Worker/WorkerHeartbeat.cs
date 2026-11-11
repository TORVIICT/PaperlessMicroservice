using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ocr.Worker;

/// <summary>
/// Refreshes a heartbeat file independently of job processing, so a long OCR
/// operation does not make the container look unhealthy.
/// </summary>
public sealed class WorkerHeartbeat : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly string _healthFile;
    private readonly ILogger<WorkerHeartbeat> _logger;

    public WorkerHeartbeat(IConfiguration configuration, ILogger<WorkerHeartbeat> logger)
    {
        _healthFile = configuration["Worker:HealthFile"] ?? "/tmp/ocr-worker-heartbeat";
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(_healthFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await File.WriteAllTextAsync(
                    _healthFile,
                    DateTimeOffset.UtcNow.ToString("O"),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not update worker heartbeat file {HealthFile}.", _healthFile);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
