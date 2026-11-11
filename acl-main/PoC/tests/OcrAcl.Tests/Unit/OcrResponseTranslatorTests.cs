using OcrAcl.Api.Contracts;
using OcrAcl.Api.Translation;
using OcrAcl.Application.Models;

namespace OcrAcl.Tests.Unit;

public sealed class OcrResponseTranslatorTests
{
    [Fact]
    [Trait("TestCaseId", "TC-UNIT-005")]
    [Trait("Category", "UNIT")]
    public void Translate_InternalResult_MapsContractFields()
    {
        var result = new OcrResult("doc-001", "hello world", 2, "eng", 0.95, "SIMULATED-OCR");

        var response = OcrResponseTranslator.ToResponse(result);

        Assert.Equal("doc-001", response.DocumentId);                        // CT-09
        Assert.Equal("completed", response.Status);                          // CT-09
        Assert.Equal("hello world", response.Text);                          // CT-09
        Assert.Equal(2, response.PageCount);                                 // CT-09
        Assert.Equal("eng", response.Language);                              // CT-09
        Assert.Equal(0.95, response.Confidence);                             // CT-09
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-006")]
    [Trait("Category", "UNIT")]
    public void Translate_InternalResult_OmitsEngineDescriptor()
    {
        var result = new OcrResult("doc-001", "hello world", 2, "eng", 0.95, "SIMULATED-OCR");

        var response = OcrResponseTranslator.ToResponse(result);

        var propertyNames = typeof(OcrDocumentResponse)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Confidence", "DocumentId", "Language", "PageCount", "Status", "Text" },
            propertyNames);                                                  // CT-09, D-23
    }
}
