using OcrAcl.Application.Ports;

namespace OcrAcl.Tests.Fixtures;

/// <summary>
/// Deterministic fake <see cref="IClock"/> for unit tests.
/// </summary>
public sealed class FakeClock : IClock
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// Creates a fake clock fixed at the given time.
    /// </summary>
    public FakeClock(DateTimeOffset? utcNow = null)
    {
        _utcNow = utcNow ?? DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _utcNow;

    /// <summary>
    /// Advances the clock by the specified amount.
    /// </summary>
    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

    /// <summary>
    /// Sets the clock to a specific time.
    /// </summary>
    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        _utcNow = _utcNow.Add(delay);
        return Task.CompletedTask;
    }
}