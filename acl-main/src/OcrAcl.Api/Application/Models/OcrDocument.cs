namespace OcrAcl.Application.Models;

public sealed record OcrDocument
{
    public required Guid DocumentId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required OcrStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed record OcrDocumentText
{
    public required Guid DocumentId { get; init; }
    public required string Text { get; init; }
    public required int PageCount { get; init; }
    public required long DurationMs { get; init; }
    public required string EngineVersion { get; init; }
}
