namespace OcrAcl.Api.Contracts;

public sealed class OcrDocumentRequest
{
    public string? DocumentId { get; init; }

    public string? FileName { get; init; }

    public string? MimeType { get; init; }

    public string? ContentBase64 { get; init; }

    public string? Language { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool LanguageSpecified { get; set; }
}
