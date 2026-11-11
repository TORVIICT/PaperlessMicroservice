using OcrAcl.Application.Models;
using OcrAcl.Infrastructure;

namespace OcrAcl.Tests.Unit;

public sealed class SimulatedOcrEngineTests
{
    private static readonly SimulatedOcrEngine Engine = new();

    private static OcrDocument Document(string content = "SGVsbG8gd29ybGQ=", string documentId = "doc-001") =>
        new(documentId, "invoice.pdf", "application/pdf", Convert.FromBase64String(content), "eng");

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-007")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_SameInput_ReturnsIdenticalResult()
    {
        var document = Document();

        var first = await Engine.RecognizeAsync(document, CancellationToken.None);
        var second = await Engine.RecognizeAsync(document, CancellationToken.None);

        Assert.Equal(first.Text, second.Text);                              // D-20, D-21
        Assert.Equal(first.PageCount, second.PageCount);                    // D-21
        Assert.Equal(first.Confidence, second.Confidence);                  // D-21
        Assert.Equal(first.Language, second.Language);                      // D-21
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-008")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_DifferentContent_ReturnsDifferentText()
    {
        var first = await Engine.RecognizeAsync(Document("SGVsbG8gd29ybGQ="), CancellationToken.None);
        var second = await Engine.RecognizeAsync(Document("Z29vZGJ5ZSB3b3JsZA=="), CancellationToken.None);

        Assert.NotEqual(first.Text, second.Text);                           // D-20
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-009")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_PageCount_IsAtLeastOne()
    {
        var result = await Engine.RecognizeAsync(Document(), CancellationToken.None);

        Assert.True(result.PageCount >= 1);                                 // CT-09, D-21
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-010")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_Confidence_IsBetweenZeroAndOne()
    {
        var result = await Engine.RecognizeAsync(Document(), CancellationToken.None);

        Assert.InRange(result.Confidence, 0.0, 1.0);                        // CT-09, D-21
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-011")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_Result_IsMarkedAsSimulated()
    {
        var result = await Engine.RecognizeAsync(Document(), CancellationToken.None);

        Assert.Equal("SIMULATED-OCR", result.EngineDescriptor);             // T-18, D-23
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-012")]
    [Trait("Category", "UNIT")]
    public async Task Recognize_PreCancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Engine.RecognizeAsync(Document(), cts.Token));            // D-27
    }
}
