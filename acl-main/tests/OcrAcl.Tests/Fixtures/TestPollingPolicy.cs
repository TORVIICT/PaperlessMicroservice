namespace OcrAcl.Tests.Fixtures;

using OcrAcl.Application.Ports;
using OcrAcl.Infrastructure.Ocr;
using System.Collections.Concurrent;

/// <summary>
/// Test-specific polling policy that uses a controlled delay mechanism for deterministic integration testing.
/// Uses a ConcurrentQueue of delays that can be pre-configured for each test scenario.
/// </summary>
public sealed class TestPollingPolicy : PollingPolicy
{
    private readonly ConcurrentQueue<TimeSpan> _delayQueue = new();
    private readonly FixedTimeProvider _fixedClock;
    private readonly TimeSpan _testGlobalTimeout = TimeSpan.FromSeconds(1); // Very short for tests

    public TestPollingPolicy(FixedTimeProvider fixedClock) : base(fixedClock)
    {
        _fixedClock = fixedClock;
    }

    /// <summary>
    /// Queues a specific delay for the next wait call.
    /// </summary>
    public void QueueDelay(TimeSpan delay)
    {
        _delayQueue.Enqueue(delay);
    }

    /// <summary>
    /// Queues multiple delays for sequential wait calls.
    /// </summary>
    public void QueueDelays(params TimeSpan[] delays)
    {
        foreach (var delay in delays)
        {
            _delayQueue.Enqueue(delay);
        }
    }

    /// <summary>
    /// Clears all queued delays.
    /// </summary>
    public void ClearDelays()
    {
        while (_delayQueue.TryDequeue(out _)) { }
    }

    public override Task WaitInitialAsync(CancellationToken cancellationToken = default)
    {
        if (_delayQueue.TryDequeue(out var delay))
        {
            _fixedClock.Advance(delay);
            return Task.CompletedTask;
        }
        // Fallback: instant delay for tests
        return Task.CompletedTask;
    }

    public override Task WaitPollDelayAsync(int attemptNumber, CancellationToken cancellationToken = default)
    {
        if (_delayQueue.TryDequeue(out var delay))
        {
            _fixedClock.Advance(delay);
            return Task.CompletedTask;
        }
        // Fallback: instant delay for tests
        return Task.CompletedTask;
    }

    public override Task WaitTransientRetryAsync(CancellationToken cancellationToken = default)
    {
        if (_delayQueue.TryDequeue(out var delay))
        {
            _fixedClock.Advance(delay);
            return Task.CompletedTask;
        }
        // Fallback: instant delay for tests
        return Task.CompletedTask;
    }

    public override CancellationToken CreateGlobalTimeoutToken(CancellationToken externalToken)
    {
        var timeoutCts = new CancellationTokenSource(_testGlobalTimeout);
        return CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token).Token;
    }
}