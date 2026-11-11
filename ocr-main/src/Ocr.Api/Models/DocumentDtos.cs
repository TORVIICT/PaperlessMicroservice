namespace Ocr.Api.Models;

public record ApiErrorResponse(
    string Code,
    string Message,
    Guid? DocumentId = null,
    object? Details = null);

public record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int? PageCount,
    object? Error);

public record DocumentTextResponse(
    Guid DocumentId,
    string Text,
    int PageCount,
    long DurationMs,
    string EngineVersion);

public record PaginatedDocumentsResponse(
    IReadOnlyList<DocumentResponse> Items,
    int Total,
    int Page,
    int PageSize);
