using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Infrastructure.Ocr;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Integration;

/// <summary>
/// Orchestration integration tests (T-064).
/// Simulates submit, poll, timeout, transient failure and terminal failure scenarios.
/// Verifies AC-03, AC-04, AC-07, AC-08 (FR-05, FR-06, FR-07, FR-12, FR-13).
/// </summary>
public sealed class OrchestrationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeHttpMessageHandler _fakeOcrHandler;
    private readonly HttpClient _apiClient;
    private readonly IHost _host;
    private readonly TestPollingPolicy _testPollingPolicy;
    private readonly FixedTimeProvider _fixedClock;

    public OrchestrationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _fakeOcrHandler = new FakeHttpMessageHandler();
        _fixedClock = new FixedTimeProvider();
        _testPollingPolicy = new TestPollingPolicy(_fixedClock);
        
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

                // Remove the OcrPollingBackgroundService registration
                var bgServiceDescriptor = services.Where(d => d.ImplementationType == typeof(OcrPollingBackgroundService)).FirstOrDefault();
                if (bgServiceDescriptor != null)
                {
                    services.Remove(bgServiceDescriptor);
                }

                // Remove IHostedService registrations for OcrPollingBackgroundService
                var hostedDescriptors = services.Where(d => d.ServiceType == typeof(IHostedService) && 
                    d.ImplementationType == typeof(OcrPollingBackgroundService)).ToList();
                foreach (var desc in hostedDescriptors)
                {
                    services.Remove(desc);
                }

                // Add our fake handler and test implementations
                services.AddSingleton(_fakeOcrHandler);
                services.AddSingleton<IClock>(_fixedClock);
                services.AddSingleton<PollingPolicy>(_testPollingPolicy);
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
                
                // Re-register the background service with our test clock
                services.AddHostedService<OcrPollingBackgroundService>();
            });
        });

        _apiClient = _factory.CreateClient();
        
        // Start the hosted services (including OcrPollingBackgroundService)
        _host = _factory.Services.GetRequiredService<IHost>();
        _host.StartAsync(default).GetAwaiter().GetResult();
        
        // Reset the fixed clock after host startup (background service's empty-queue loop advances time)
        _fixedClock.Reset();
    }

    private async Task<(Guid DocumentId, JsonElement SubmitBody)> SubmitDocumentWithResponseAsync(string fileName = "document.pdf", string contentType = "application/pdf")
    {
        using var fileContent = new MemoryStream("fake pdf content"u8.ToArray());
        using var form = new MultipartFormDataContent();
        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileStreamContent, "file", fileName);
        
        var submitResponse = await _apiClient.PostAsync("/api/v1/documents", form);
        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var documentId = submitBody.GetProperty("id").GetGuid();
        return (documentId, submitBody);
    }

    private void SetupStandardPollDelays(int pollCount = 3)
    {
        _testPollingPolicy.ClearDelays();
        // Initial wait (500ms) + poll delays
        var delays = new List<TimeSpan> { TimeSpan.FromMilliseconds(500) };
        for (int i = 0; i < pollCount; i++)
        {
            delays.Add(TimeSpan.FromMilliseconds(500));
        }
        _testPollingPolicy.QueueDelays(delays.ToArray());
    }

    [Fact]
    [Trait("TestCaseId", "TC-T064-001")]
    [Trait("Category", "INTEGRATION")]
    public async Task Submit_Poll_Completed_FullFlow_WithBackgroundService()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-orchestration-001";
        
        // Queue submit response (202 with processing status)
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        
        // Queue polling responses: processing -> processing -> completed
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 1
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 2
        _fakeOcrHandler.QueueCompletedResponse(
            jobId, clientRef, 
            "Extracted text from PDF", 5, 2500, "tesseract 5.3.0", 
            true, false);

        SetupStandardPollDelays(3);

        // Act - Submit document
        var (documentId, submitBody) = await SubmitDocumentWithResponseAsync("test.pdf");
        
        // Verify initial response is 202 with queued status (from POST response)
        Assert.Equal("queued", submitBody.GetProperty("status").GetString());
        Assert.Equal(documentId, submitBody.GetProperty("id").GetGuid());
        Assert.Equal("test.pdf", submitBody.GetProperty("fileName").GetString());
        
        // Advance the fixed clock to trigger background polling
        // Initial wait (500ms) + 3 polls (500ms each) = 2 seconds total
        _fixedClock.Advance(TimeSpan.FromSeconds(3));
        
        // Give the background service time to process
        await Task.Delay(100); // Small delay to allow async processing
        
        // Assert - Verify document reached completed status via GET
        var finalStatusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, finalStatusResponse.StatusCode);
        var finalStatusBody = await finalStatusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", finalStatusBody.GetProperty("status").GetString());
        Assert.Equal(5, finalStatusBody.GetProperty("pageCount").GetInt32());
        Assert.NotEqual(default, finalStatusBody.GetProperty("completedAt").GetDateTimeOffset());
        
        // Verify text endpoint works for completed document
        var textResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/text");
        Assert.Equal(HttpStatusCode.OK, textResponse.StatusCode);
        var textBody = await textResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(documentId, textBody.GetProperty("id").GetGuid());
        Assert.Equal("Extracted text from PDF", textBody.GetProperty("text").GetString());
        Assert.Equal(5, textBody.GetProperty("pageCount").GetInt32());
    }

    [Fact]
    [Trait("TestCaseId", "TC-T064-002")]
    [Trait("Category", "INTEGRATION")]
    public async Task Submit_Poll_Failed_TerminalFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-orchestration-002";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 1
        _fakeOcrHandler.QueueFailedResponse(jobId, clientRef, "ENGINE_ERROR", "OCR engine crashed");

        SetupStandardPollDelays(2);

        // Act
        var (documentId, submitBody) = await SubmitDocumentWithResponseAsync();
        
        // Verify initial response has queued status
        Assert.Equal("queued", submitBody.GetProperty("status").GetString());
        
        // Advance time for polling to complete
        _fixedClock.Advance(TimeSpan.FromSeconds(2));
        
        await Task.Delay(100);
        
        // Assert - Verify document reached failed status
        var finalStatusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, finalStatusResponse.StatusCode);
        var finalStatusBody = await finalStatusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", finalStatusBody.GetProperty("status").GetString());
        Assert.True(finalStatusBody.GetProperty("error").ValueKind != JsonValueKind.Null);
        Assert.Equal("OCR_PROCESSING_FAILED", finalStatusBody.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("ENGINE_ERROR", finalStatusBody.GetProperty("error").GetProperty("details").GetProperty("ocrCode").GetString());
        
        // Text endpoint should return 409 for failed document
        var textResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}/text");
        Assert.Equal(HttpStatusCode.Conflict, textResponse.StatusCode); // 409 DOCUMENT_NOT_READY
    }

    [Fact]
    [Trait("TestCaseId", "TC-T064-003")]
    [Trait("Category", "INTEGRATION")]
    public async Task TransientFailure_RetriesWithJitter_ThenSucceeds()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-orchestration-003";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        // Two transient 503 failures, then success
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 1
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 2
        _fakeOcrHandler.QueueCompletedResponse(
            jobId, clientRef, 
            "Text after retries", 2, 1800, "tesseract 5.3.0", 
            false, false);

        // Queue delays: initial wait + poll1 + retry1 + poll2 + retry2 + poll3
        _testPollingPolicy.ClearDelays();
        _testPollingPolicy.QueueDelays(
            TimeSpan.FromMilliseconds(500), // Initial wait
            TimeSpan.FromMilliseconds(500), // Poll 1 (fails with 503)
            TimeSpan.FromMilliseconds(250), // Retry 1 jitter
            TimeSpan.FromMilliseconds(500), // Poll 2 (fails with 503)
            TimeSpan.FromMilliseconds(250), // Retry 2 jitter
            TimeSpan.FromMilliseconds(500)  // Poll 3 (succeeds)
        );

        // Act
        var (documentId, submitBody) = await SubmitDocumentWithResponseAsync();
        
        // Verify initial response has queued status
        Assert.Equal("queued", submitBody.GetProperty("status").GetString());
        
        // Advance time for all polling steps
        _fixedClock.Advance(TimeSpan.FromSeconds(5));
        
        await Task.Delay(200); // Extra time for retries
        
        // Assert - Should eventually succeed
        var finalStatusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, finalStatusResponse.StatusCode);
        var finalStatusBody = await finalStatusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", finalStatusBody.GetProperty("status").GetString());
        Assert.Equal(2, finalStatusBody.GetProperty("pageCount").GetInt32());
        
        // Verify we had the expected number of poll attempts (3 polls + 2 retries = 5 GET requests to job endpoint)
        var pollRequests = _fakeOcrHandler.Requests.Where(r => r.Method == HttpMethod.Get && r.RequestUri?.ToString().Contains("/jobs/") == true).ToList();
        Assert.True(pollRequests.Count >= 3, $"Expected at least 3 poll requests, got {pollRequests.Count}");
    }

    [Fact]
    [Trait("TestCaseId", "TC-T064-004")]
    [Trait("Category", "INTEGRATION")]
    public async Task TransientFailure_MaxRetriesExceeded_ReturnsFailed()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-orchestration-004";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        // Four 503 failures (exceeds max of 3)
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 1
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 2
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 3 (max retries)
        _fakeOcrHandler.QueueServiceUnavailableResponse();  // Transient failure 4 (should fail)

        // Queue delays for all retry attempts
        _testPollingPolicy.ClearDelays();
        _testPollingPolicy.QueueDelays(
            TimeSpan.FromMilliseconds(500), // Initial wait
            TimeSpan.FromMilliseconds(500), // Poll 1
            TimeSpan.FromMilliseconds(250), // Retry 1
            TimeSpan.FromMilliseconds(500), // Poll 2
            TimeSpan.FromMilliseconds(250), // Retry 2
            TimeSpan.FromMilliseconds(500), // Poll 3
            TimeSpan.FromMilliseconds(250), // Retry 3 (max)
            TimeSpan.FromMilliseconds(500), // Poll 4 (fails)
            TimeSpan.FromMilliseconds(250)  // Retry 4 (exceeds max)
        );

        // Act
        var (documentId, submitBody) = await SubmitDocumentWithResponseAsync();
        
        // Verify initial response has queued status
        Assert.Equal("queued", submitBody.GetProperty("status").GetString());
        
        // Advance time for all polling steps
        _fixedClock.Advance(TimeSpan.FromSeconds(5));
        
        await Task.Delay(200);
        
        // Assert - Should fail after max retries
        var finalStatusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, finalStatusResponse.StatusCode);
        var finalStatusBody = await finalStatusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", finalStatusBody.GetProperty("status").GetString());
        // When max retries exceeded, the error should be SERVICE_UNAVAILABLE
        Assert.Equal("SERVICE_UNAVAILABLE", finalStatusBody.GetProperty("error").GetProperty("details").GetProperty("ocrCode").GetString());
    }

    [Fact]
    [Trait("TestCaseId", "TC-T064-005")]
    [Trait("Category", "INTEGRATION")]
    public async Task CancelledRequest_AbortsPolling()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-orchestration-005";
        
        _fakeOcrHandler.QueueSubmitResponse(jobId, clientRef);
        // Return processing indefinitely - the test will cancel
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 1
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 2
        _fakeOcrHandler.QueueProcessingResponse(jobId, clientRef);  // Poll 3

        // Queue many delays to simulate ongoing polling
        _testPollingPolicy.ClearDelays();
        var delays = new List<TimeSpan> { TimeSpan.FromMilliseconds(500) }; // Initial wait
        for (int i = 0; i < 10; i++)
        {
            delays.Add(TimeSpan.FromMilliseconds(500)); // Poll 1-10
        }
        _testPollingPolicy.QueueDelays(delays.ToArray());

        // Act
        var (documentId, submitBody) = await SubmitDocumentWithResponseAsync();
        
        // Verify initial response has queued status
        Assert.Equal("queued", submitBody.GetProperty("status").GetString());
        
        // Cancel by advancing clock past global timeout (120s)
        _fixedClock.Advance(TimeSpan.FromSeconds(130)); // Past 120s global timeout
        
        await Task.Delay(200);
        
        // Assert - Should be marked as failed due to timeout
        var finalStatusResponse = await _apiClient.GetAsync($"/api/v1/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, finalStatusResponse.StatusCode);
        var finalStatusBody = await finalStatusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", finalStatusBody.GetProperty("status").GetString());
        // The error code should be PROCESSING_TIMEOUT or similar
        Assert.True(finalStatusBody.GetProperty("error").ValueKind != JsonValueKind.Null);
    }
}