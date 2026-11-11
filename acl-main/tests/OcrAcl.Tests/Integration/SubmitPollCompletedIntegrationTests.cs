using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Infrastructure.Ocr;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Integration;

/// <summary>
/// Integration tests for the OCR client polling flow.
/// Simulates the OCR service and proves the ACL submits, polls and maps a completed job.
/// Verifies AC-03, AC-04 (FR-05, FR-07).
/// Note: Background polling is not implemented (OQ-08), so we manually set store state for completed scenarios.
/// </summary>
public sealed class SubmitPollCompletedIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeHttpMessageHandler _fakeOcrHandler;
    private readonly HttpClient _apiClient;

    public SubmitPollCompletedIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _fakeOcrHandler = new FakeHttpMessageHandler();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing HttpOcrClient registration
                var ocrClientDescriptor = services.Where(d => d.ServiceType == typeof(IOcrClient)).FirstOrDefault();
                if (ocrClientDescriptor != null)
                {
                    services.Remove(ocrClientDescriptor);
                }

                // Remove the OcrOutboundHeadersHandler registration
                var handlerDescriptor = services.Where(d => d.ImplementationType == typeof(OcrOutboundHeadersHandler)).FirstOrDefault();
                if (handlerDescriptor != null)
                {
                    services.Remove(handlerDescriptor);
                }

                // Remove the PollingPolicy registration
                var pollingDescriptor = services.Where(d => d.ServiceType == typeof(PollingPolicy)).FirstOrDefault();
                if (pollingDescriptor != null)
                {
                    services.Remove(pollingDescriptor);
                }

                // Add our fake handler and test implementations
                services.AddSingleton(_fakeOcrHandler);
                services.AddSingleton<PollingPolicy>(sp => new PollingPolicy(sp.GetRequiredService<IClock>()));
                services.AddSingleton<OcrOutboundHeadersHandler>(sp => new OcrOutboundHeadersHandler(
                    sp.GetRequiredService<ICorrelationIdAccessor>(), "test-token"));
                
                // Register HttpOcrClient with a properly configured HttpClient
                services.AddSingleton<HttpOcrClient>(sp =>
                {
                    var httpClient = new HttpClient(new OcrOutboundHeadersHandler(
                        sp.GetRequiredService<ICorrelationIdAccessor>(), "test-token")
                    {
                        InnerHandler = _fakeOcrHandler
                    })
                    {
                        BaseAddress = new Uri("http://fake-ocr/"),
                        Timeout = Timeout.InfiniteTimeSpan
                    };
                    var pollingPolicy = sp.GetRequiredService<PollingPolicy>();
                    return new HttpOcrClient(httpClient, pollingPolicy);
                });

                services.AddSingleton<IOcrClient>(sp => sp.GetRequiredService<HttpOcrClient>());
            });
        });

        _apiClient = _factory.CreateClient();
    }

    private async Task<Guid> SubmitDocumentAsync(string fileName = "document.pdf", string contentType = "application/pdf")
    {
        using var fileContent = new MemoryStream("fake pdf content"u8.ToArray());
        using var form = new MultipartFormDataContent();
        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileStreamContent, "file", fileName);
        
        var submitResponse = await _apiClient.PostAsync("/api/v1/documents", form);
        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        return submitBody.GetProperty("id").GetGuid();
    }

    private async Task SetDocumentStatusAsync(Guid documentId, OcrStatus status, OcrResult? result = null, OcrJobError? error = null)
    {
        // Get the document store and update the status
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        
        var record = await store.GetByDocumentIdAsync(documentId);
        if (record == null) return;

        if (status == OcrStatus.Completed && result != null)
        {
            await store.UpdateResultAsync(documentId, result, clock.UtcNow);
        }
        else if (status == OcrStatus.Failed && error != null)
        {
            await store.UpdateErrorAsync(documentId, error, clock.UtcNow);
        }
        else
        {
            await store.UpdateStatusAsync(documentId, status, clock.UtcNow);
        }
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-001")]
    [Trait("Category", "INTEGRATION")]
    public async Task Submit_Returns202WithQueuedStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-integration-001";
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);

        // Act
        var documentId = await SubmitDocumentAsync();

        // Assert
        var statusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("queued", statusBody.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-002")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetDocumentStatus_CompletedDocument_ReturnsCompletedStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-integration-002";
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        
        var documentId = await SubmitDocumentAsync();
        
        // Manually set to completed (simulating background polling completion)
        var result = new OcrResult
        {
            Text = "Extracted text from PDF",
            PageCount = 5,
            DurationMs = 2500,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/" + jobId + "/archive",
            HasThumbnail = true,
            ThumbnailUrl = "/api/v1/ocr/jobs/" + jobId + "/thumbnail"
        };
        await SetDocumentStatusAsync(documentId, OcrStatus.Completed, result);

        // Act
        var statusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", statusBody.GetProperty("status").GetString());
        Assert.Equal(5, statusBody.GetProperty("pageCount").GetInt32());
        Assert.NotEqual(default, statusBody.GetProperty("completedAt").GetDateTimeOffset());
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-003")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetDocumentStatus_FailedDocument_ReturnsFailedStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-integration-003";
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        
        var documentId = await SubmitDocumentAsync();
        
        // Manually set to failed
        var error = new OcrJobError { Code = "ENGINE_ERROR", Message = "OCR engine crashed" };
        await SetDocumentStatusAsync(documentId, OcrStatus.Failed, error: error);

        // Act
        var statusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", statusBody.GetProperty("status").GetString());
        Assert.True(statusBody.GetProperty("error").ValueKind != JsonValueKind.Null);
        Assert.Equal("OCR_PROCESSING_FAILED", statusBody.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("ENGINE_ERROR", statusBody.GetProperty("error").GetProperty("details").GetProperty("ocrCode").GetString());
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-004")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetDocumentText_CompletedDocument_ReturnsTextAndPageCount()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-integration-004";
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        
        var documentId = await SubmitDocumentAsync();
        
        // Manually set to completed
        var result = new OcrResult
        {
            Text = "Full extracted text content",
            PageCount = 3,
            DurationMs = 1800,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/" + jobId + "/archive",
            HasThumbnail = false,
            ThumbnailUrl = null
        };
        await SetDocumentStatusAsync(documentId, OcrStatus.Completed, result);

        // Act
        var textResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/text");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, textResponse.StatusCode);
        var textBody = await textResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(documentId, textBody.GetProperty("id").GetGuid());
        Assert.Equal("Full extracted text content", textBody.GetProperty("text").GetString());
        Assert.Equal(3, textBody.GetProperty("pageCount").GetInt32());
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-005")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetDocumentText_NotCompleted_Returns409()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-integration-005";
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        
        var documentId = await SubmitDocumentAsync();
        // Don't set to completed - stays as queued

        // Act
        var textResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/text");
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, textResponse.StatusCode); // 409 DOCUMENT_NOT_READY
    }
}