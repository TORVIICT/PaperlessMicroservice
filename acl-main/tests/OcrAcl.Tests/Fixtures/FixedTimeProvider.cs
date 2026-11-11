namespace OcrAcl.Tests.Fixtures;

using OcrAcl.Application.Ports;

/// <summary>
/// Fixed time provider for deterministic testing of time-dependent logic.
/// Allows tests to control time flow without real waits.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider, IClock
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public DateTimeOffset UtcNow => _utcNow;

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

    /// <summary>
    /// Sets the clock to a specific time.
    /// </summary>
    public void Set(DateTimeOffset time) => _utcNow = time;

    /// <summary>
    /// Resets the clock to the current real time.
    /// </summary>
    public void Reset() => _utcNow = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        _utcNow = _utcNow.Add(delay);
        return Task.CompletedTask;
    }
}