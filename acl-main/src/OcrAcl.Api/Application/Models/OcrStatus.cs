namespace OcrAcl.Application.Models;

/// <summary>
/// OCR job status values per OCR-SRC §3.3 and FE-SRC §5.
/// </summary>
public enum OcrStatus
{
    /// <summary>Job is queued but not yet processing (mentioned in OCR-SRC §3 as 'pending').</summary>
    Pending,

    /// <summary>Job is actively being processed.</summary>
    Processing,

    /// <summary>Job completed successfully.</summary>
    Completed,

    /// <summary>Job failed definitively.</summary>
    Failed
}

/// <summary>
/// Status transition rules for OCR jobs.
/// Failed is terminal; no invented transitions; unknown status is rejected.
/// </summary>
public static class OcrStatusTransitions
{
    /// <summary>
    /// Returns true if the status is terminal (no further transitions allowed).
    /// </summary>
    public static bool IsTerminal(this OcrStatus status) =>
        status == OcrStatus.Completed || status == OcrStatus.Failed;

    /// <summary>
    /// Returns true if the status is non-terminal (job still in progress).
    /// </summary>
    public static bool IsNonTerminal(this OcrStatus status) =>
        !status.IsTerminal();

    /// <summary>
    /// Validates that a status transition is allowed.
    /// </summary>
    /// <param name="from">Current status.</param>
    /// <param name="to">Target status.</param>
    /// <returns>True if transition is valid.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="from"/> or <paramref name="to"/> is not a defined <see cref="OcrStatus"/> value.</exception>
    public static bool CanTransition(OcrStatus from, OcrStatus to)
    {
        // Reject unknown status values explicitly
        if (!Enum.IsDefined(typeof(OcrStatus), from))
            throw new ArgumentOutOfRangeException(nameof(from), $"Unknown status value: {from}");
        if (!Enum.IsDefined(typeof(OcrStatus), to))
            throw new ArgumentOutOfRangeException(nameof(to), $"Unknown status value: {to}");

        // No transition from terminal states
        if (from.IsTerminal())
            return false;

        // Allowed transitions per OCR-SRC §3.3 and FE-SRC §5:
        // Pending -> Processing
        // Processing -> Completed, Failed
        return (from, to) switch
        {
            (OcrStatus.Pending, OcrStatus.Processing) => true,
            (OcrStatus.Processing, OcrStatus.Completed) => true,
            (OcrStatus.Processing, OcrStatus.Failed) => true,
            _ => false
        };
    }

    /// <summary>
    /// Maps an OCR service status string to <see cref="OcrStatus"/>.
    /// </summary>
    /// <param name="ocrStatus">Status string from OCR service (pending, processing, completed, failed).</param>
    /// <returns>The corresponding <see cref="OcrStatus"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the status string is not recognized.</exception>
    public static OcrStatus FromOcrString(string ocrStatus)
    {
        return ocrStatus.ToLowerInvariant() switch
        {
            "pending" => OcrStatus.Pending,
            "processing" => OcrStatus.Processing,
            "completed" => OcrStatus.Completed,
            "failed" => OcrStatus.Failed,
            _ => throw new ArgumentException($"Unrecognized OCR status: '{ocrStatus}'", nameof(ocrStatus))
        };
    }

    /// <summary>
    /// Maps an <see cref="OcrStatus"/> to the FE document model status string.
    /// </summary>
    /// <param name="status">Internal OCR status.</param>
    /// <returns>FE status string (queued, processing, completed, failed).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the status is not defined.</exception>
    public static string ToFeStatus(this OcrStatus status)
    {
        return status switch
        {
            OcrStatus.Pending => "queued",      // FE expects 'queued' for initial state (INC-09)
            OcrStatus.Processing => "processing",
            OcrStatus.Completed => "completed",
            OcrStatus.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(status), $"Unknown status: {status}")
        };
    }
}