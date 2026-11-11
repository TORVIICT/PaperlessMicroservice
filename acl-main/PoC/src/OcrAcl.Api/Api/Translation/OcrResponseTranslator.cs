using OcrAcl.Api.Contracts;
using OcrAcl.Application.Models;

namespace OcrAcl.Api.Translation;

public static class OcrResponseTranslator
{
    public const string CompletedStatus = "completed";

    public static OcrDocumentResponse ToResponse(OcrResult result)
    {
        return new OcrDocumentResponse
        {
            DocumentId = result.DocumentId,
            Status = CompletedStatus,
            Text = result.Text,
            PageCount = result.PageCount,
            Language = result.Language,
            Confidence = result.Confidence
        };
    }
}
