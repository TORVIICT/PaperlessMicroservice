using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Ocr.Infrastructure.Workers;

/// <summary>
/// Timing knobs shared by the embedded worker and the external worker.
/// Invalid or missing values fall back to safe defaults.
/// </summary>
public sealed record WorkerSettings(
    TimeSpan PollingInterval,
    TimeSpan StaleJobTimeout,
    TimeSpan StaleCheckInterval)
{
    public static WorkerSettings FromConfiguration(IConfiguration configuration) =>
        new(
            PollingInterval: TimeSpan.FromSeconds(
                ReadPositiveInt(configuration, "Worker:PollingIntervalSeconds", 2)),
            StaleJobTimeout: TimeSpan.FromMinutes(
                ReadPositiveInt(configuration, "Worker:StaleJobTimeoutMinutes", 30)),
            StaleCheckInterval: TimeSpan.FromMinutes(
                ReadPositiveInt(configuration, "Worker:StaleCheckIntervalMinutes", 5)));

    private static int ReadPositiveInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
        value > 0
            ? value
            : fallback;
}
