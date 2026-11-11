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
/// Integration tests for archive streaming end-to-end.
/// Proves GET /documents/{id}/archive streams the OCR archive without exposing the OCR URL.
/// Verifies AC-05 (FR-10).
/// Note: Background polling is not implemented (OQ-08), so we manually set store state for completed scenarios.
/// </summary>
public sealed class ArchiveStreamingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeHttpMessageHandler _fakeOcrHandler;
    private readonly HttpClient _apiClient;

    public ArchiveStreamingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _fakeOcrHandler = new FakeHttpMessageHandler();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing registrations
                var ocrClientDescriptor = services.Where(d => d.ServiceType == typeof(IOcrClient)).FirstOrDefault();
                if (ocrClientDescriptor != null) services.Remove(ocrClientDescriptor);

                var handlerDescriptor = services.Where(d => d.ImplementationType == typeof(OcrOutboundHeadersHandler)).FirstOrDefault();
                if (handlerDescriptor != null) services.Remove(handlerDescriptor);

                var pollingDescriptor = services.Where(d => d.ServiceType == typeof(PollingPolicy)).FirstOrDefault();
                if (pollingDescriptor != null) services.Remove(pollingDescriptor);

                // Add test implementations
                services.AddSingleton(_fakeOcrHandler);
                services.AddSingleton<PollingPolicy>(sp => new PollingPolicy(sp.GetRequiredService<IClock>()));
                services.AddSingleton<OcrOutboundHeadersHandler>(sp => new OcrOutboundHeadersHandler(
                    sp.GetRequiredService<ICorrelationIdAccessor>(), "test-token"));
                
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

    private async Task SetDocumentCompletedWithArchiveAsync(Guid documentId, Guid jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 2,
            DurationMs = 1000,
            EngineVersion = "engine",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/" + jobId + "/archive",
            HasThumbnail = false,
            ThumbnailUrl = null
        };
        await store.UpdateResultAsync(documentId, result, clock.UtcNow);
        
        // Also set up the fake OCR response for archive download
        _fakeOcrHandler.QueueArchiveResponse("fake PDF archive content for testing"u8.ToArray());
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-006")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetArchive_CompletedDocumentWithArchive_StreamsPdf()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-archive-001";
        var archiveContent = "fake PDF archive content for testing"u8.ToArray();
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        var documentId = await SubmitDocumentAsync();
        
        await SetDocumentCompletedWithArchiveAsync(documentId, jobId);
        // Override the archive content for this specific test
        _fakeOcrHandler.QueueArchiveResponse(archiveContent);

        // Act - Get archive
        var archiveResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/archive");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.Equal("application/pdf", archiveResponse.Content.Headers.ContentType?.MediaType);
        
        var streamedContent = await archiveResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(archiveContent, streamedContent);
        
        // Verify OCR URL not exposed in response
        var responseString = await archiveResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fake-ocr", responseString);
        Assert.DoesNotContain("archiveDownloadUrl", responseString);
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-007")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetArchive_DocumentNotFound_Returns404()
    {
        // Act
        var archiveResponse = await _apiClient.GetAsync($"/api/v1/documents/{Guid.NewGuid()}/archive");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-008")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetArchive_DocumentNotCompleted_Returns409()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-archive-002";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        var documentId = await SubmitDocumentAsync();
        // Don't set to completed - stays as queued

        // Act
        var archiveResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/archive");
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, archiveResponse.StatusCode); // 409 DOCUMENT_NOT_READY
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-009")]
    [Trait("Category", "INTEGRATION")]
    public async Task GetArchive_OcrReturns404_Returns409()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-archive-003";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        var documentId = await SubmitDocumentAsync();
        
        // Set completed with archive flag but OCR returns 404
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 500,
            EngineVersion = "engine",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/" + jobId + "/archive",
            HasThumbnail = false,
            ThumbnailUrl = null
        };
        await store.UpdateResultAsync(documentId, result, clock.UtcNow);
        
        // Queue only the 404 response
        _fakeOcrHandler.QueueNotFoundResponse("Archive not generated yet");

        // Act
        var archiveResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/archive");
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, archiveResponse.StatusCode); // 409 DOCUMENT_NOT_READY
    }
}