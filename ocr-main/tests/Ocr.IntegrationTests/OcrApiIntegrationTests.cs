using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ocr.IntegrationTests;

public class OcrApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OcrApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/ocr/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthLiveAndReady_ReturnsOk()
    {
        var liveResponse = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        var readyResponse = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
    }

    [Fact]
    public async Task CreateJobAndGetJob_FullLifecycle_Succeeds_AndArtifactsCanBeDownloaded()
    {
        // 1. Prepare form content with a sample PDF/document
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 sample invoice text content for integration test"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", "sample-invoice.pdf");
        form.Add(new StringContent("doc-client-ref-123"), "clientReference");
        form.Add(new StringContent("eng"), "language");
        form.Add(new StringContent("skip"), "mode");

        // 2. Submit: POST /api/v1/ocr/jobs
        var postResponse = await _client.PostAsync("/api/v1/ocr/jobs", form);

        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var postBody = await postResponse.Content.ReadAsStringAsync();
        using var postDoc = JsonDocument.Parse(postBody);
        var jobId = postDoc.RootElement.GetProperty("jobId").GetString();
        Assert.NotNull(jobId);

        // 3. Query: GET /api/v1/ocr/jobs/{jobId} with resilient polling
        JsonDocument? completedDoc = null;
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var getResponse = await _client.GetAsync($"/api/v1/ocr/jobs/{jobId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var getBody = await getResponse.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(getBody);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (status == "completed")
            {
                completedDoc = doc;
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(completedDoc);
        using (completedDoc)
        {
            Assert.Equal("completed", completedDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal("doc-client-ref-123", completedDoc.RootElement.GetProperty("clientReference").GetString());

            var result = completedDoc.RootElement.GetProperty("result");
            var text = result.GetProperty("text").GetString();
            Assert.NotNull(text);
            Assert.NotEmpty(text);
            Assert.True(result.GetProperty("pageCount").GetInt32() >= 1);

            // 4. Verify Archived PDF download endpoint
            Assert.True(result.GetProperty("hasArchivedPdf").GetBoolean());
            var archiveUrl = result.GetProperty("archiveDownloadUrl").GetString();
            Assert.NotNull(archiveUrl);

            var archiveResponse = await _client.GetAsync(archiveUrl);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
            Assert.Equal("application/pdf", archiveResponse.Content.Headers.ContentType?.MediaType);
            var archiveBytes = await archiveResponse.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(archiveBytes);

            // 5. Verify Thumbnail download endpoint
            Assert.True(result.GetProperty("hasThumbnail").GetBoolean());
            var thumbUrl = result.GetProperty("thumbnailUrl").GetString();
            Assert.NotNull(thumbUrl);

            var thumbResponse = await _client.GetAsync(thumbUrl);
            Assert.Equal(HttpStatusCode.OK, thumbResponse.StatusCode);
            Assert.Contains(thumbResponse.Content.Headers.ContentType?.MediaType, new[] { "image/png", "image/webp" });
            var thumbBytes = await thumbResponse.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(thumbBytes);
        }
    }
}
