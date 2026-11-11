namespace OcrAcl.Api.Contracts;

public sealed class OcrDocumentResponse
{
    public string DocumentId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public int PageCount { get; init; }

    public string Language { get; init; } = string.Empty;

    public double Confidence { get; init; }
}
