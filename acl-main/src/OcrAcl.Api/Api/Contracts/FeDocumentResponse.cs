namespace OcrAcl.Api.Contracts;

/// <summary>
/// FE document model returned by the API (FE-SRC §5).
/// Matches the Gateway/Frontend contract exactly.
/// </summary>
public sealed record FeDocumentResponse
{
    /// <summary>Document identifier (UUID, ACL-owned per D-34).</summary>
    public required Guid Id { get; init; }

    /// <summary>Original file name.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type (application/pdf, image/png, image/jpeg).</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Document status: queued, processing, completed, failed.</summary>
    public required string Status { get; init; }

    /// <summary>Creation timestamp (ISO 8601 UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Completion timestamp (null while processing).</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Page count (null until completed).</summary>
    public int? PageCount { get; init; }

    /// <summary>Error info when status is failed.</summary>
    public FeErrorObject? Error { get; init; }
}

/// <summary>
/// Error object inside the document model (FE-SRC §6, D-42).
/// Same shape as the error envelope but without documentId.
/// </summary>
public sealed record FeErrorObject
{
    /// <summary>Error code (INVALID_FILE, UNSUPPORTED_FILE_TYPE, FILE_TOO_LARGE, DOCUMENT_NOT_FOUND, DOCUMENT_NOT_READY, OCR_PROCESSING_FAILED, SERVICE_UNAVAILABLE).</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional additional details (e.g., original OCR error code).</summary>
    public object? Details { get; init; }
}

/// <summary>
/// FE error envelope for API error responses (FE-SRC §6, D-42).
/// </summary>
public sealed record FeErrorEnvelope
{
    /// <summary>Error code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string Message { get; init; }

    /// <summary>Document ID associated with the error (null for upload validation errors).</summary>
    public Guid? DocumentId { get; init; }

    /// <summary>Optional additional details.</summary>
    public object? Details { get; init; }
}

/// <summary>
/// Text extraction response (FE-E4, D-43).
/// </summary>
public sealed record FeDocumentTextResponse
{
    /// <summary>Document identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Extracted text content.</summary>
    public required string Text { get; init; }

    /// <summary>Page count.</summary>
    public required int PageCount { get; init; }
}