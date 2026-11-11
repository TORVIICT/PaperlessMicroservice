using System.IO;
using System.Collections.Generic;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Unit;

/// <summary>
/// Unit tests for the <see cref="SubmitDocumentHandler"/> use case.
/// </summary>
public sealed class SubmitDocumentHandlerTests
{
    private readonly FakeIdGenerator _idGenerator;
    private readonly FakeClock _clock;

    public SubmitDocumentHandlerTests()
    {
        _idGenerator = new FakeIdGenerator();
        _clock = new FakeClock(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    }

    private static SubmitDocumentInput CreateValidInput() => new(
        new MemoryStream("fake content"u8.ToArray()),
        "test.pdf",
        "application/pdf",
        "eng",
        "skip");

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-016")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_ValidInput_ReturnsJobWithGeneratedClientReference()
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = CreateValidInput();

        var result = await handler.ExecuteAsync(input, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.StartsWith("doc-ref-test", result.ClientReference);
        Assert.Equal(OcrStatus.Processing, result.Status);
        Assert.NotEqual(default, result.CreatedAt); // CreatedAt comes from OCR service
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-017")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_ValidInput_CallsOcrClientWithCorrectParameters()
    {
        var capturedParams = new List<(Stream FileContent, string FileName, string ContentType, string ClientReference, string Language, string Mode)>();
        var ocrClient = new FakeOcrClient(
            submitFactory: p =>
            {
                capturedParams.Add((p.FileContent, p.FileName, p.ContentType, p.ClientReference, p.Language, p.Mode));
                return new OcrJob
                {
                    JobId = Guid.NewGuid(),
                    ClientReference = p.ClientReference,
                    Status = OcrStatus.Processing,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            });
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);

        var input = CreateValidInput();
        await handler.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(capturedParams);
        var (fileContent, fileName, contentType, clientRef, language, mode) = capturedParams[0];
        Assert.Equal("test.pdf", fileName);
        Assert.Equal("application/pdf", contentType);
        Assert.StartsWith("doc-ref-test", clientRef);
        Assert.Equal("eng", language);
        Assert.Equal("skip", mode);
        Assert.NotNull(fileContent);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-018")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var observedToken = CancellationToken.None;

        var ocrClient = new FakeOcrClient(
            submitFactory: p =>
            {
                observedToken = cts.Token;
                return new OcrJob
                {
                    JobId = Guid.NewGuid(),
                    ClientReference = p.ClientReference,
                    Status = OcrStatus.Processing,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            });
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);

        await handler.ExecuteAsync(CreateValidInput(), cts.Token);

        Assert.Equal(cts.Token, observedToken);
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-019")]
    [Trait("Category", "UNIT")]
    [MemberData(nameof(EmptyFileContentCases))]
    public async Task ExecuteAsync_EmptyFileContent_ThrowsArgumentException(Stream fileContent, string expectedMessage)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            fileContent,
            "test.pdf",
            "application/pdf",
            "eng",
            "skip");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(input, CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message);
    }

    public static IEnumerable<object[]> EmptyFileContentCases =>
        new List<object[]>
        {
            new object[] { Stream.Null, "File content is required." },
            new object[] { new MemoryStream(), "File content is required." }
        };

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-020")]
    [Trait("Category", "UNIT")]
    [InlineData("", "File name is required.")]
    [InlineData("   ", "File name is required.")]
    public async Task ExecuteAsync_InvalidFileName_ThrowsArgumentException(string fileName, string expectedMessage)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            new MemoryStream([1, 2, 3]),
            fileName,
            "application/pdf",
            "eng",
            "skip");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(input, CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-021")]
    [Trait("Category", "UNIT")]
    [InlineData("application/msword", "Unsupported content type")]
    [InlineData("image/gif", "Unsupported content type")]
    [InlineData("text/plain", "Unsupported content type")]
    [InlineData("", "Unsupported content type")]
    public async Task ExecuteAsync_UnsupportedContentType_ThrowsArgumentException(string contentType, string expectedMessage)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            new MemoryStream([1, 2, 3]),
            "test.pdf",
            contentType,
            "eng",
            "skip");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(input, CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-022")]
    [Trait("Category", "UNIT")]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public async Task ExecuteAsync_AllowedContentType_DoesNotThrow(string contentType)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            new MemoryStream([1, 2, 3]),
            "test.pdf",
            contentType,
            "eng",
            "skip");

        var result = await handler.ExecuteAsync(input, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, result.JobId);
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-023")]
    [Trait("Category", "UNIT")]
    [InlineData("", "Language is required.")]
    [InlineData("   ", "Language is required.")]
    public async Task ExecuteAsync_InvalidLanguage_ThrowsArgumentException(string language, string expectedMessage)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            new MemoryStream([1, 2, 3]),
            "test.pdf",
            "application/pdf",
            language,
            "skip");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(input, CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-024")]
    [Trait("Category", "UNIT")]
    [InlineData("", "Mode is required.")]
    [InlineData("   ", "Mode is required.")]
    public async Task ExecuteAsync_InvalidMode_ThrowsArgumentException(string mode, string expectedMessage)
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);
        var input = new SubmitDocumentInput(
            new MemoryStream([1, 2, 3]),
            "test.pdf",
            "application/pdf",
            "eng",
            mode);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(input, CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-025")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_ReturnsCompletedJob_WhenOcrClientReturnsCompleted()
    {
        var ocrClient = FakeOcrClient.CompletedJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);

        var result = await handler.ExecuteAsync(CreateValidInput(), CancellationToken.None);

        Assert.Equal(OcrStatus.Completed, result.Status);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-026")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_ReturnsFailedJob_WhenOcrClientReturnsFailed()
    {
        var ocrClient = FakeOcrClient.FailedJob("ENGINE_ERROR", "Engine crashed");
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);

        var result = await handler.ExecuteAsync(CreateValidInput(), CancellationToken.None);

        Assert.Equal(OcrStatus.Failed, result.Status);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-027")]
    [Trait("Category", "UNIT")]
    public async Task ExecuteAsync_GeneratesUniqueClientReference_PerCall()
    {
        var ocrClient = FakeOcrClient.ProcessingJob();
        var handler = new SubmitDocumentHandler(ocrClient, _idGenerator, _clock);

        var results = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var result = await handler.ExecuteAsync(CreateValidInput(), CancellationToken.None);
            results.Add(result.ClientReference);
        }

        Assert.Equal(3, results.Distinct().Count());
        Assert.All(results, r => Assert.StartsWith("doc-ref-test", r));
    }
}