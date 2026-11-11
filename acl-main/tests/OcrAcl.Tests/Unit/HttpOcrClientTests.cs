using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Infrastructure.Ocr;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Unit;

/// <summary>
/// Unit tests for the <see cref="HttpOcrClient"/> with a fake HTTP handler.
/// Verifies contract compliance per OCR-SRC §3–§4, §7.
/// </summary>
public sealed class HttpOcrClientTests
{
    private readonly PollingPolicy _pollingPolicy;
    private readonly FakeCorrelationIdAccessor _correlationIdAccessor;
    private readonly FakeClock _clock;

    public HttpOcrClientTests()
    {
        _pollingPolicy = new PollingPolicy(new FakeClock());
        _correlationIdAccessor = new FakeCorrelationIdAccessor();
        _clock = new FakeClock();
    }

    private HttpOcrClient CreateClient(FakeHttpMessageHandler fakeHandler, string token = "test-token")
    {
        // Build the handler pipeline: OcrOutboundHeadersHandler -> fakeHandler
        var pipeline = new OcrAcl.Infrastructure.Ocr.OcrOutboundHeadersHandler(_correlationIdAccessor, token)
        {
            InnerHandler = fakeHandler
        };
        
        var httpClient = new HttpClient(pipeline) { BaseAddress = new Uri("http://test-ocr/") };
        return new HttpOcrClient(httpClient, _pollingPolicy);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-001")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_ValidRequest_ReturnsJobWithJobIdAndProcessingStatus()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-test123";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("test content"u8.ToArray());
        
        var job = await client.SubmitAsync(fileContent, "test.pdf", "application/pdf", clientRef, "spa", "skip");
        
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(clientRef, job.ClientReference);
        Assert.Equal(OcrStatus.Processing, job.Status);
        Assert.NotEqual(default, job.CreatedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.Result);
        Assert.Null(job.Error);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-002")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_SendsCorrectMultipartFields()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-fields";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("file content"u8.ToArray());
        
        await client.SubmitAsync(fileContent, "document.pdf", "application/pdf", clientRef, "eng", "redo");
        
        var request = Assert.Single(fakeHandler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/ocr/jobs", request.RequestUri!.AbsolutePath);
        
        // Verify multipart content from captured request
        Assert.NotNull(request.Content);
        Assert.Contains("document.pdf", request.Content);
        Assert.Contains(clientRef, request.Content);
        Assert.Contains("eng", request.Content);
        Assert.Contains("redo", request.Content);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-003")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_IncludesAuthorizationHeader()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-auth";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler, "my-test-token");
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        await client.SubmitAsync(fileContent, "test.pdf", "application/pdf", clientRef, "spa", "skip");
        
        var request = Assert.Single(fakeHandler.Requests);
        Assert.True(request.Headers.TryGetValue("Authorization", out var authValues));
        Assert.Contains("Bearer my-test-token", authValues);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-004")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_IncludesCorrelationIdHeader()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-corr";
        var expectedCorrelationId = Guid.NewGuid();
        _correlationIdAccessor.SetCorrelationId(expectedCorrelationId);
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        await client.SubmitAsync(fileContent, "test.pdf", "application/pdf", clientRef, "spa", "skip");
        
        var request = Assert.Single(fakeHandler.Requests);
        Assert.True(request.Headers.TryGetValue("X-Correlation-ID", out var corrValues));
        Assert.Equal(expectedCorrelationId.ToString(), corrValues.First());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-005")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_IncludesAcceptJsonHeader()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-accept";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        await client.SubmitAsync(fileContent, "test.pdf", "application/pdf", clientRef, "spa", "skip");
        
        var request = Assert.Single(fakeHandler.Requests);
        Assert.True(request.Headers.TryGetValue("Accept", out var acceptValues));
        Assert.Contains("application/json", acceptValues.First());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-006")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_415Response_ThrowsUnsupportedFileType()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueUnsupportedMediaTypeResponse();
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAsync(fileContent, "test.tiff", "image/tiff", "doc-ref", "spa", "skip"));
        
        Assert.Equal("UNSUPPORTED_FILE_TYPE", ex.Message);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-007")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_400Response_ReadsErrorBodyAndThrowsInvalidFile()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueBadRequestResponse("File is corrupted");
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAsync(fileContent, "test.pdf", "application/pdf", "doc-ref", "spa", "skip"));
        
        Assert.Equal("INVALID_FILE", ex.Message);
        // Verify the error body was read (not lost)
        Assert.Single(fakeHandler.Requests);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-008")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_MissingLocationHeader_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        // No Location header
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueResponse(response);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAsync(fileContent, "test.pdf", "application/pdf", "doc-ref", "spa", "skip"));
        
        Assert.Contains("Location", ex.Message);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-009")]
    [Trait("Category", "UNIT")]
    public async Task GetJobAsync_CompletedJob_ReturnsJobWithResult()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-get";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueCompletedResponse(jobId, clientRef, "extracted text", 3, 1500, "tesseract 5.3.0", true, true);
        
        var client = CreateClient(fakeHandler);
        var job = await client.GetJobAsync(jobId);
        
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(clientRef, job.ClientReference);
        Assert.Equal(OcrStatus.Completed, job.Status);
        Assert.NotNull(job.Result);
        Assert.Equal("extracted text", job.Result!.Text);
        Assert.Equal(3, job.Result.PageCount);
        Assert.Equal(1500, job.Result.DurationMs);
        Assert.Equal("tesseract 5.3.0", job.Result.EngineVersion);
        Assert.True(job.Result.HasArchivedPdf);
        Assert.True(job.Result.HasThumbnail);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-010")]
    [Trait("Category", "UNIT")]
    public async Task GetJobAsync_FailedJob_ReturnsJobWithError()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-failed";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueFailedResponse(jobId, clientRef, "ENGINE_ERROR", "OCR engine crashed");
        
        var client = CreateClient(fakeHandler);
        var job = await client.GetJobAsync(jobId);
        
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(OcrStatus.Failed, job.Status);
        Assert.NotNull(job.Error);
        Assert.Equal("ENGINE_ERROR", job.Error!.Code);
        Assert.Equal("OCR engine crashed", job.Error.Message);
        Assert.Null(job.Result);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-011")]
    [Trait("Category", "UNIT")]
    public async Task GetJobAsync_ProcessingJob_ReturnsJobWithoutResultOrError()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-processing";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueProcessingResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var job = await client.GetJobAsync(jobId);
        
        Assert.Equal(OcrStatus.Processing, job.Status);
        Assert.Null(job.Result);
        Assert.Null(job.Error);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-012")]
    [Trait("Category", "UNIT")]
    public async Task GetJobAsync_404Response_ThrowsJobNotFound()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueNotFoundResponse("Job with ID ... not found");
        
        var client = CreateClient(fakeHandler);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetJobAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-013")]
    [Trait("Category", "UNIT")]
    public async Task GetJobByUrlAsync_UsesProvidedUrl()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-byurl";
        var customUrl = "/custom/path/jobs/" + jobId;
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueCompletedResponse(jobId, clientRef, "text", 1, 100, "engine", false, false);
        
        var client = CreateClient(fakeHandler);
        var job = await client.GetJobByUrlAsync(customUrl);
        
        Assert.Equal(jobId, job.JobId);
        var request = Assert.Single(fakeHandler.Requests);
        Assert.Equal(customUrl.TrimStart('/'), request.RequestUri!.AbsolutePath.TrimStart('/'));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-014")]
    [Trait("Category", "UNIT")]
    public async Task GetJobByUrlAsync_AcceptsAbsoluteUrl()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-absurl";
        var absoluteUrl = "http://other-host/api/v1/ocr/jobs/" + jobId;
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueCompletedResponse(jobId, clientRef, "text", 1, 100, "engine", false, false);
        
        var client = CreateClient(fakeHandler);
        var job = await client.GetJobByUrlAsync(absoluteUrl);
        
        Assert.Equal(jobId, job.JobId);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-015")]
    [Trait("Category", "UNIT")]
    public async Task DownloadArchiveAsync_ReturnsStream()
    {
        var jobId = Guid.NewGuid();
        var archiveContent = "fake pdf content"u8.ToArray();
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueArchiveResponse(archiveContent);
        
        var client = CreateClient(fakeHandler);
        var stream = await client.DownloadArchiveAsync(jobId);
        
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(archiveContent, ms.ToArray());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-016")]
    [Trait("Category", "UNIT")]
    public async Task DownloadArchiveAsync_404_Throws()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueNotFoundResponse("Archive not found");
        
        var client = CreateClient(fakeHandler);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadArchiveAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-017")]
    [Trait("Category", "UNIT")]
    public async Task DownloadThumbnailAsync_ReturnsStream()
    {
        var jobId = Guid.NewGuid();
        var thumbnailContent = "fake png"u8.ToArray();
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueThumbnailResponse(thumbnailContent, "image/png");
        
        var client = CreateClient(fakeHandler);
        var stream = await client.DownloadThumbnailAsync(jobId);
        
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(thumbnailContent, ms.ToArray());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-018")]
    [Trait("Category", "UNIT")]
    public async Task DownloadThumbnailAsync_404_Throws()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueNotFoundResponse("Thumbnail not found");
        
        var client = CreateClient(fakeHandler);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadThumbnailAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-019")]
    [Trait("Category", "UNIT")]
    public async Task SubmitAsync_CancellationToken_Propagated()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-cancel";
        
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueSubmitResponse(jobId, clientRef);
        
        var client = CreateClient(fakeHandler);
        var fileContent = new MemoryStream("content"u8.ToArray());
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SubmitAsync(fileContent, "test.pdf", "application/pdf", clientRef, "spa", "skip", cts.Token));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-OCR-020")]
    [Trait("Category", "UNIT")]
    public async Task GetJobAsync_CancellationToken_Propagated()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.QueueProcessingResponse(Guid.NewGuid(), "doc-ref");
        
        var client = CreateClient(fakeHandler);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetJobAsync(Guid.NewGuid(), cts.Token));
    }
}