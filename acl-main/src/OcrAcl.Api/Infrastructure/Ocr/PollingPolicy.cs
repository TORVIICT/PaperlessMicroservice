namespace OcrAcl.Infrastructure.Ocr;

using OcrAcl.Application.Ports;

/// <summary>
/// Implements the OCR polling policy as defined in OCR-SRC §4.
/// Pure component with no external dependencies; testable with a fake clock.
/// </summary>
public class PollingPolicy
{
    /// <summary>
    /// Initial wait after 202 before the first poll (500 ms).
    /// </summary>
    public static readonly TimeSpan InitialWait = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Global operation timeout (120 s per OCR-SRC §4 "for example 120 s").
    /// </summary>
    public static readonly TimeSpan GlobalTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum transient poll retries with jitter (3 per OCR-SRC §4).
    /// </summary>
    public const int MaxTransientRetries = 3;

    /// <summary>
    /// Jitter range for transient retries (±250 ms).
    /// </summary>
    public static readonly TimeSpan JitterRange = TimeSpan.FromMilliseconds(250);

    private readonly IClock _clock;
    private readonly Random _random;

    /// <summary>
    /// Creates a new polling policy.
    /// </summary>
    /// <param name="clock">Clock for time operations (allows deterministic testing).</param>
    public PollingPolicy(IClock clock)
    {
        _clock = clock;
        _random = new Random();
    }

    /// <summary>
    /// Gets the delay before the next poll attempt.
    /// Sequence: 500 ms ×3, 1 s ×7, 2 s … (OCR-SRC §4).
    /// </summary>
    /// <param name="attemptNumber">The poll attempt number (1-based).</param>
    /// <returns>The delay to wait before this attempt.</returns>
    public static TimeSpan GetPollDelay(int attemptNumber)
    {
        return attemptNumber switch
        {
            >= 1 and <= 3 => TimeSpan.FromMilliseconds(500),
            >= 4 and <= 10 => TimeSpan.FromSeconds(1),
            >= 11 => TimeSpan.FromSeconds(2),
            _ => throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be >= 1")
        };
    }

    /// <summary>
    /// Gets a jittered delay for transient retry.
    /// </summary>
    /// <returns>A random delay within the jitter range.</returns>
    public virtual TimeSpan GetJitteredDelay()
    {
        var jitterMs = _random.Next(-(int)JitterRange.TotalMilliseconds, (int)JitterRange.TotalMilliseconds + 1);
        return TimeSpan.FromMilliseconds(jitterMs);
    }

    /// <summary>
    /// Waits for the initial delay after job submission.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task WaitInitialAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(InitialWait, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for the specified poll attempt delay.
    /// </summary>
    /// <param name="attemptNumber">The poll attempt number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task WaitPollDelayAsync(int attemptNumber, CancellationToken cancellationToken = default)
    {
        var delay = GetPollDelay(attemptNumber);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for a jittered transient retry delay.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task WaitTransientRetryAsync(CancellationToken cancellationToken = default)
    {
        var jitter = GetJitteredDelay();
        await Task.Delay(jitter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a linked cancellation token that cancels after the global timeout.
    /// </summary>
    /// <param name="externalToken">External cancellation token (e.g., from the HTTP request).</param>
    /// <returns>A cancellation token that cancels when either the external token cancels or the global timeout expires.</returns>
    public virtual CancellationToken CreateGlobalTimeoutToken(CancellationToken externalToken)
    {
        var timeoutCts = new CancellationTokenSource(GlobalTimeout);
        return CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token).Token;
    }
}