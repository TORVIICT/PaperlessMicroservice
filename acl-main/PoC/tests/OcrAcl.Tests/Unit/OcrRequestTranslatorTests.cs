using OcrAcl.Api.Contracts;
using OcrAcl.Api.Translation;

namespace OcrAcl.Tests.Unit;

public sealed class OcrRequestTranslatorTests
{
    private static OcrDocumentRequest ValidRequest(string? documentId = "doc-001", string? fileName = "invoice.pdf",
        string? mimeType = "application/pdf", string? contentBase64 = "SGVsbG8gd29ybGQ=", string? language = "spa") =>
        new()
        {
            DocumentId = documentId,
            FileName = fileName,
            MimeType = mimeType,
            ContentBase64 = contentBase64,
            Language = language,
            LanguageSpecified = language is not null
        };

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-001")]
    [Trait("Category", "UNIT")]
    public void Translate_ValidRequest_MapsEveryField()
    {
        var request = ValidRequest();

        var ok = OcrRequestTranslator.TryTranslate(request, out var document, out var invalidFields);

        Assert.True(ok);
        Assert.Empty(invalidFields);
        Assert.NotNull(document);
        Assert.Equal("doc-001", document!.DocumentId);                       // CT-03
        Assert.Equal("invoice.pdf", document.FileName);                      // CT-04
        Assert.Equal("application/pdf", document.MimeType);                  // CT-05
        Assert.Equal("spa", document.Language);                              // CT-07
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x20, 0x77, 0x6f, 0x72, 0x6c, 0x64 }, document.Content); // CT-06
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-002")]
    [Trait("Category", "UNIT")]
    public void Translate_AbsentLanguage_DefaultsToEng()
    {
        var request = ValidRequest(language: null);
        var ok = OcrRequestTranslator.TryTranslate(request, out var document, out _);

        Assert.True(ok);
        Assert.Equal("eng", document!.Language);                             // CT-07
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-003")]
    [Trait("Category", "UNIT")]
    public void Translate_TrimsDocumentIdAndFileName()
    {
        var request = ValidRequest(documentId: "  doc-002  ", fileName: " report.pdf ");

        var ok = OcrRequestTranslator.TryTranslate(request, out var document, out _);

        Assert.True(ok);
        Assert.Equal("doc-002", document!.DocumentId);                       // A-08
        Assert.Equal("report.pdf", document.FileName);                       // A-08
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-004")]
    [Trait("Category", "UNIT")]
    public void Translate_DocumentIdBoundary64_IsAccepted()
    {
        var documentId = new string('a', 64);
        var request = ValidRequest(documentId: documentId);

        var ok = OcrRequestTranslator.TryTranslate(request, out var document, out _);

        Assert.True(ok);
        Assert.Equal(documentId, document!.DocumentId);                      // CT-03
    }
}
