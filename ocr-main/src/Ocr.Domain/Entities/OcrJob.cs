using Ocr.Domain.Enums;

namespace Ocr.Domain.Entities;

public record OcrJobResult(
    string Text,
    int PageCount,
    long DurationMs,
    string EngineVersion,
    string? ArchivedPdfPath = null,
    string? ThumbnailPath = null)
{
    public OcrJobResult() : this(string.Empty, 0, 0, string.Empty, null, null) { }
}

public record OcrJobError(
    string Code,
    string Message)
{
    public OcrJobError() : this(string.Empty, string.Empty) { }
}

public sealed class OcrJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string ClientReference { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/pdf";
    public long SizeBytes { get; init; }
    public string? FileStoragePath { get; set; }
    public string Language { get; init; } = "eng";
    public string Mode { get; init; } = "skip";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public OcrJobResult? Result { get; set; }
    public OcrJobError? Error { get; set; }
}
