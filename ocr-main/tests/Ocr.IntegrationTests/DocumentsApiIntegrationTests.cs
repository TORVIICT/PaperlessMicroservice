using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ocr.IntegrationTests;

public class DocumentsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DocumentsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadDocument_ValidPdf_Returns202Accepted_AndMatchesContractModel()
    {
        using var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 test document content for frontend contract");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", "factura_test.pdf");

        var response = await _client.PostAsync("/api/v1/documents", form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/api/v1/documents/", response.Headers.Location.ToString());

        var jsonStr = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonStr);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out var idProp));
        Assert.NotNull(idProp.GetString());
        Assert.Equal("factura_test.pdf", root.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", root.GetProperty("contentType").GetString());
        Assert.Equal(bytes.Length, root.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("queued", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("completedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("pageCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task UploadDocument_MissingFile_Returns400_WithContractErrorFormat()
    {
        using var form = new MultipartFormDataContent();

        var response = await _client.PostAsync("/api/v1/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonStr = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonStr);

        Assert.Equal("INVALID_FILE", doc.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task UploadDocument_UnsupportedExtension_Returns415_WithContractErrorFormat()
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("echo hello"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-msdownload");
        form.Add(fileContent, "file", "script.exe");

        var response = await _client.PostAsync("/api/v1/documents", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var jsonStr = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonStr);

        Assert.Equal("UNSUPPORTED_FILE_TYPE", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task FullDocumentLifecycle_EndToEnd_MatchesAllFrontendEndpoints()
    {
        // 1. Upload Document: POST /api/v1/documents
        using var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 sample commercial invoice text for OCR processing");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", "contrato_frontend_sample.pdf");

        var postResponse = await _client.PostAsync("/api/v1/documents", form);
        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);

        var postBody = await postResponse.Content.ReadAsStringAsync();
        using var postDoc = JsonDocument.Parse(postBody);
        var docId = postDoc.RootElement.GetProperty("id").GetString();
        Assert.NotNull(docId);

        // 2. Poll Document: GET /api/v1/documents/{id}
        JsonDocument? completedDoc = null;
        for (var i = 0; i < 30; i++)
        {
            var getRes = await _client.GetAsync($"/api/v1/documents/{docId}");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            var body = await getRes.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
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
            var root = completedDoc.RootElement;
            Assert.Equal("completed", root.GetProperty("status").GetString());
            Assert.NotNull(root.GetProperty("completedAt").GetString());
            Assert.True(root.GetProperty("pageCount").GetInt32() >= 1);
        }

        // 3. Get Extracted Text: GET /api/v1/documents/{id}/text
        var textRes = await _client.GetAsync($"/api/v1/documents/{docId}/text");
        Assert.Equal(HttpStatusCode.OK, textRes.StatusCode);
        var textBody = await textRes.Content.ReadAsStringAsync();
        using var textDoc = JsonDocument.Parse(textBody);
        Assert.Equal(docId, textDoc.RootElement.GetProperty("documentId").GetString());
        Assert.NotEmpty(textDoc.RootElement.GetProperty("text").GetString()!);
        Assert.True(textDoc.RootElement.GetProperty("pageCount").GetInt32() >= 1);

        // 4. Download Original: GET /api/v1/documents/{id}/original
        var origRes = await _client.GetAsync($"/api/v1/documents/{docId}/original");
        Assert.Equal(HttpStatusCode.OK, origRes.StatusCode);
        var origBytes = await origRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes.Length, origBytes.Length);

        // 5. Download Searchable PDF/A: GET /api/v1/documents/{id}/archive
        var archRes = await _client.GetAsync($"/api/v1/documents/{docId}/archive");
        Assert.Equal(HttpStatusCode.OK, archRes.StatusCode);
        Assert.Equal("application/pdf", archRes.Content.Headers.ContentType?.MediaType);
        var archBytes = await archRes.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(archBytes);

        // 6. List Documents: GET /api/v1/documents
        var listRes = await _client.GetAsync("/api/v1/documents?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listBody = await listRes.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listBody);
        var items = listDoc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.True(listDoc.RootElement.GetProperty("total").GetInt32() >= 1);

        // 7. Delete Document: DELETE /api/v1/documents/{id}
        var delRes = await _client.DeleteAsync($"/api/v1/documents/{docId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 8. Confirm 404 after deletion: GET /api/v1/documents/{id}
        var getDeletedRes = await _client.GetAsync($"/api/v1/documents/{docId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedRes.StatusCode);
        var notFoundBody = await getDeletedRes.Content.ReadAsStringAsync();
        using var notFoundDoc = JsonDocument.Parse(notFoundBody);
        Assert.Equal("DOCUMENT_NOT_FOUND", notFoundDoc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListDocuments_InvalidStatus_Returns400_WithAllowedValues()
    {
        var response = await _client.GetAsync("/api/v1/documents?status=banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("INVALID_STATUS", doc.RootElement.GetProperty("code").GetString());
        var message = doc.RootElement.GetProperty("message").GetString();
        Assert.Contains("queued", message);
        Assert.Contains("processing", message);
        Assert.Contains("completed", message);
        Assert.Contains("failed", message);
    }
}
