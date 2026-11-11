namespace OcrAcl.Application.Ports;

using OcrAcl.Application.Models;

/// <summary>
/// ACL-owned document state port.
/// Keeps the mapping between ACL documentId, OCR jobId, and clientReference,
/// plus upload metadata (fileName, contentType, sizeBytes) and status.
/// Implemented by Infrastructure adapters (in-memory per D-34).
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Creates a new document record with initial state.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier (UUID).</param>
    /// <param name="jobId">OCR job identifier.</param>
    /// <param name="clientReference">Opaque reference sent to OCR (≤ 64, [a-z0-9_-]).</param>
    /// <param name="fileName">Original file name from upload.</param>
    /// <param name="contentType">MIME type (application/pdf, image/png, image/jpeg).</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="initialStatus">Initial document status (typically Pending/Queued).</param>
    /// <param name="createdAt">Creation timestamp.</param>
    /// <param name="pollingUrl">OCR polling URL from Location header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(
        Guid documentId,
        Guid jobId,
        string clientReference,
        string fileName,
        string contentType,
        long sizeBytes,
        OcrStatus initialStatus,
        DateTimeOffset createdAt,
        string pollingUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by its ACL documentId.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document record, or null if not found.</returns>
    Task<DocumentRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by its OCR jobId.
    /// </summary>
    /// <param name="jobId">OCR job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document record, or null if not found.</returns>
    Task<DocumentRecord?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by its clientReference.
    /// </summary>
    /// <param name="clientReference">Opaque reference sent to OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document record, or null if not found.</returns>
    Task<DocumentRecord?> GetByClientReferenceAsync(string clientReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the document status and optionally the completion timestamp.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier.</param>
    /// <param name="status">New status.</param>
    /// <param name="completedAt">Completion timestamp (set when terminal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and updated.</returns>
    Task<bool> UpdateStatusAsync(Guid documentId, OcrStatus status, DateTimeOffset? completedAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the document with the OCR job result.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier.</param>
    /// <param name="result">OCR result payload.</param>
    /// <param name="completedAt">Completion timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and updated.</returns>
    Task<bool> UpdateResultAsync(Guid documentId, OcrResult result, DateTimeOffset completedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the document with an error when the OCR job fails.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier.</param>
    /// <param name="error">OCR job error.</param>
    /// <param name="completedAt">Failure timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and updated.</returns>
    Task<bool> UpdateErrorAsync(Guid documentId, OcrJobError error, DateTimeOffset completedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the monolith document ID for the ACL document.
    /// Used for idempotency and tracking delivery to the monolith.
    /// </summary>
    /// <param name="documentId">ACL-owned document identifier.</param>
    /// <param name="monolithDocumentId">The monolith's document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and updated.</returns>
    Task<bool> UpdateMonolithDocumentIdAsync(Guid documentId, int monolithDocumentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal document record kept by the ACL store.
/// Matches the fields needed for the FE document model (FE-SRC §5) plus OCR mapping.
/// </summary>
public sealed record DocumentRecord
{
    /// <summary>ACL-owned document identifier (UUID).</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>OCR job identifier.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Opaque reference sent to OCR (≤ 64, [a-z0-9_-]).</summary>
    public required string ClientReference { get; init; }

    /// <summary>Original file name from upload.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type (application/pdf, image/png, image/jpeg).</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Current document status.</summary>
    public required OcrStatus Status { get; init; }

    /// <summary>Creation timestamp (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Completion timestamp (null while running).</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>OCR result payload (when completed).</summary>
    public OcrResult? Result { get; init; }

    /// <summary>OCR job error (when failed).</summary>
    public OcrJobError? Error { get; init; }

    /// <summary>OCR polling URL from Location header (used for background polling).</summary>
    public required string PollingUrl { get; init; }

    /// <summary>Monolith document ID (set after successful delivery).</summary>
    public int? MonolithDocumentId { get; init; }
}