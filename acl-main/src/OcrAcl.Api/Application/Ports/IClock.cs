namespace OcrAcl.Application.Ports;

using System.Threading;

/// <summary>
/// Clock abstraction for time-dependent operations.
/// Use this instead of <see cref="DateTime.UtcNow"/> in Application/Api response paths (T-18, T-29).
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Delays for the specified duration using the clock's time source.
    /// Allows test clocks to control time flow without real waits.
    /// </summary>
    /// <param name="delay">The duration to delay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation using <see cref="TimeProvider"/> (available in .NET 8+).
/// </summary>
public sealed class TimeProviderClock(TimeProvider timeProvider) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.Delay(delay, cancellationToken);
}