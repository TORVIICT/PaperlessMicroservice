using OcrAcl.Application.Models;
using OcrAcl.Application.Services;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Unit;

public sealed class OcrDocumentServiceTests
{
    private static OcrDocument Document() =>
        new("doc-001", "invoice.pdf", "application/pdf", Convert.FromBase64String("SGVsbG8gd29ybGQ="), "eng");

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-013")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_ValidRequest_ReturnsEngineResult()
    {
        var engine = new RecordingOcrEngine();
        var service = new OcrDocumentService(engine);

        var result = await service.ExecuteAsync(Document(), CancellationToken.None);

        Assert.Equal("doc-001", result.DocumentId);                         // CT-09
        Assert.Equal(RecordingOcrEngine.ResultText, result.Text);           // CT-09
        Assert.Equal(RecordingOcrEngine.ResultPageCount, result.PageCount); // CT-09
        Assert.Equal("eng", result.Language);                               // CT-09
        Assert.Equal(RecordingOcrEngine.ResultConfidence, result.Confidence); // CT-09
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-014")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        var engine = new RecordingOcrEngine();
        var service = new OcrDocumentService(engine);
        using var cts = new CancellationTokenSource();

        await service.ExecuteAsync(Document(), cts.Token);

        Assert.Equal(cts.Token, engine.LastToken);                          // D-27
    }
}
