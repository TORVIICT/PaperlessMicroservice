namespace OcrAcl.Application.Ports;

/// <summary>
/// Abstraction for accessing the correlation ID in the current request context.
/// Reads inbound `X-Correlation-ID` header or generates a new one if absent.
/// Policy is configurable (OQ-10).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the correlation ID for the current request.
    /// Returns the inbound `X-Correlation-ID` if present and valid, otherwise generates a new UUID v4.
    /// </summary>
    Guid CorrelationId { get; }

    /// <summary>
    /// Sets the correlation ID explicitly (e.g., from inbound header during request initialization).
    /// </summary>
    void SetCorrelationId(Guid correlationId);
}

/// <summary>
/// Deterministic fake for testing. Returns a fixed or incrementing correlation ID.
/// </summary>
public sealed class FakeCorrelationIdAccessor : ICorrelationIdAccessor
{
    private Guid _correlationId = Guid.Empty;
    private int _counter = 0;

    /// <inheritdoc />
    public Guid CorrelationId
    {
        get
        {
            if (_correlationId != Guid.Empty)
                return _correlationId;

            // Generate deterministic test correlation IDs
            return new Guid(++_counter, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    /// <inheritdoc />
    public void SetCorrelationId(Guid correlationId) => _correlationId = correlationId;

    /// <summary>Resets the accessor for test isolation.</summary>
    public void Reset()
    {
        _correlationId = Guid.Empty;
        _counter = 0;
    }
}