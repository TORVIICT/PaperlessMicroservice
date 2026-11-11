namespace OcrAcl.Tests.Fixtures;

public static class ContractSamples
{
    // CONTRATO_OCR.md lines 39-47 (CT-10): the literal sample request of the contract.
    public const string ValidRequest =
        """
        {
          "documentId": "doc-001",
          "fileName": "invoice.pdf",
          "mimeType": "application/pdf",
          "contentBase64": "SGVsbG8gd29ybGQ=",
          "language": "eng"
        }
        """;

    // CONTRATO_OCR.md lines 51-60 (CT-09, CT-10): the literal sample success response shape.
    public const string ValidResponseShape =
        """
        {
          "documentId": "doc-001",
          "status": "completed",
          "text": "string",
          "pageCount": 1,
          "language": "eng",
          "confidence": 0.95
        }
        """;

    // CONTRATO_OCR.md lines 62-64 (CT-11): the literal health body.
    public const string HealthBody = """{ "status": "ok" }""";

    // CONTRATO_OCR.md lines 71-78 (CT-12): the literal error body sample.
    public const string ErrorShape =
        """
        {
          "error": {
            "code": "invalid_request",
            "message": "Human readable text.",
            "details": [ { "field": "mimeType", "message": "Unsupported value." } ]
          }
        }
        """;

    public const string DocumentsRoute = "/api/ocr/documents";

    public const string HealthRoute = "/api/ocr/health";

    public const string JsonMediaType = "application/json";

    public static string WithLanguage(string language) =>
        ValidRequest.Replace("\"language\": \"eng\"", $"\"language\": \"{language}\"", StringComparison.Ordinal);

    public static string WithMimeType(string mimeType) =>
        ValidRequest.Replace("\"application/pdf\"", $"\"{mimeType}\"", StringComparison.Ordinal);

    public static string WithDocumentId(string documentId) =>
        ValidRequest.Replace("\"doc-001\"", $"\"{documentId}\"", StringComparison.Ordinal);

    public static string WithFileName(string fileName) =>
        ValidRequest.Replace("\"invoice.pdf\"", $"\"{fileName}\"", StringComparison.Ordinal);

    public static string WithoutProperty(string property)
    {
        return string.Join(
            "\n",
            ValidRequest
                .Split('\n')
                .Where(line => !line.Contains($"\"{property}\":", StringComparison.Ordinal)));
    }
}
