namespace OcrAcl.Infrastructure.Ocr;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

/// <summary>
/// HTTP implementation of <see cref="IOcrClient"/> using typed HttpClient.
/// Implements OCR-SRC §3 (HTTP polling), §4 (polling policy), and §7 (contract checklist).
/// </summary>
public sealed class HttpOcrClient : IOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly PollingPolicy _pollingPolicy;

    /// <summary>
    /// Creates a new HTTP OCR client.
    /// </summary>
    /// <param name="httpClient">Typed HttpClient configured with base URL and OcrOutboundHeadersHandler.</param>
    /// <param name="pollingPolicy">Polling policy for back-off and timeouts.</param>
    public HttpOcrClient(HttpClient httpClient, PollingPolicy pollingPolicy)
    {
        _httpClient = httpClient;
        _pollingPolicy = pollingPolicy;
    }

    public async Task<OcrDocument> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/documents/{documentId}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OCR document lookup failed: {response.StatusCode}, body: {json}");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new OcrDocument
        {
            DocumentId = root.GetProperty("id").GetGuid(),
            FileName = root.GetProperty("fileName").GetString()!,
            ContentType = root.GetProperty("contentType").GetString()!,
            Status = ParseDocumentStatus(root.GetProperty("status").GetString()!),
            CreatedAt = root.GetProperty("createdAt").GetDateTimeOffset(),
            CompletedAt = root.TryGetProperty("completedAt", out var completedAt) && completedAt.ValueKind != JsonValueKind.Null
                ? completedAt.GetDateTimeOffset()
                : null
        };
    }

    public async Task<OcrDocumentText> GetTextAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/documents/{documentId}/text", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OCR text lookup failed: {response.StatusCode}, body: {json}");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new OcrDocumentText
        {
            DocumentId = root.GetProperty("documentId").GetGuid(),
            Text = root.GetProperty("text").GetString() ?? string.Empty,
            PageCount = root.GetProperty("pageCount").GetInt32(),
            DurationMs = root.GetProperty("durationMs").GetInt64(),
            EngineVersion = root.GetProperty("engineVersion").GetString() ?? string.Empty
        };
    }

    public Task<Stream> DownloadOriginalAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        DownloadFileAsync($"/api/v1/documents/{documentId}/original", "Original document", cancellationToken);

    /// <inheritdoc />
    public async Task<OcrJob> SubmitAsync(
        Stream fileContent,
        string fileName,
        string contentType,
        string clientReference,
        string language,
        string mode,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ocr/jobs");
        
        var form = new MultipartFormDataContent();
        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileStreamContent, "file", fileName);
        form.Add(new StringContent(clientReference), "clientReference");
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent(mode), "mode");
        
        request.Content = form;

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return await ParseSubmitResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }

        // Read error body before throwing (per OCR-SRC §7 item 8, INC-11)
        var errorBody = await SafeReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
        {
            throw new InvalidOperationException("UNSUPPORTED_FILE_TYPE");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // RFC 9457 error body read in errorBody
            throw new InvalidOperationException("INVALID_FILE");
        }

        // For other non-success codes, read body and throw
        throw new InvalidOperationException($"Unexpected response: {response.StatusCode}, body: {errorBody}");
    }

    /// <inheritdoc />
    public async Task<OcrJob> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/ocr/jobs/{jobId}";
        return await GetJobByUrlInternalAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OcrJob> GetJobByUrlAsync(string pollingUrl, CancellationToken cancellationToken = default)
    {
        // Use the polling URL as-is (from Location header), do not construct it
        // The URL may be relative or absolute; HttpClient handles both when BaseAddress is set
        var requestUrl = pollingUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? pollingUrl
            : pollingUrl.TrimStart('/');
        
        return await GetJobByUrlInternalAsync(requestUrl, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadArchiveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await DownloadFileAsync($"/api/v1/documents/{jobId}/archive", "Archive", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadThumbnailAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/ocr/jobs/{jobId}/thumbnail");
        
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Thumbnail not found or not yet generated");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Thumbnail download failed: {response.StatusCode}, body: {body}");
        }
        
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<OcrJob> ParseSubmitResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var location = response.Headers.Location?.ToString() ?? throw new InvalidOperationException("Missing Location header in 202 response");
        var jobId = ExtractJobIdFromLocation(location);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = OcrStatusTransitions.FromOcrString(root.GetProperty("status").GetString()!);
        var createdAt = DateTimeOffset.Parse(root.GetProperty("createdAt").GetString()!);
        var clientRef = root.GetProperty("clientReference").GetString()!;

        return new OcrJob
        {
            JobId = jobId,
            ClientReference = clientRef,
            Status = status,
            CreatedAt = createdAt,
            CompletedAt = null,
            Result = null,
            Error = null,
            PollingUrl = location
        };
    }

    private async Task<OcrJob> GetJobByUrlInternalAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Job not found");
        }

        // OCR contract: GET job returns 200 for all states (processing/completed/failed)
        // with status in body. Read body even on non-success (per OCR-SRC §3.2, §7).
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            // For transient errors (503, 504), throw HttpRequestException so background service can retry
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable || 
                response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                throw new HttpRequestException($"Transient error: {response.StatusCode}", null, response.StatusCode);
            }
            throw new InvalidOperationException($"Job status check failed: {response.StatusCode}, body: {json}");
        }
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var jobId = Guid.Parse(root.GetProperty("jobId").GetString()!);
        var clientRef = root.GetProperty("clientReference").GetString()!;
        var status = OcrStatusTransitions.FromOcrString(root.GetProperty("status").GetString()!);
        var createdAt = DateTimeOffset.Parse(root.GetProperty("createdAt").GetString()!);
        var completedAt = root.TryGetProperty("completedAt", out var completedAtElem) && completedAtElem.ValueKind != JsonValueKind.Null
            ? DateTimeOffset.Parse(completedAtElem.GetString()!)
            : (DateTimeOffset?)null;

        OcrResult? result = null;
        OcrJobError? error = null;

        if (root.TryGetProperty("result", out var resultElem) && resultElem.ValueKind != JsonValueKind.Null)
        {
            result = ParseResult(resultElem);
        }

        if (root.TryGetProperty("error", out var errorElem) && errorElem.ValueKind != JsonValueKind.Null)
        {
            error = new OcrJobError
            {
                Code = errorElem.GetProperty("code").GetString()!,
                Message = errorElem.GetProperty("message").GetString()!
            };
        }

        return new OcrJob
        {
            JobId = jobId,
            ClientReference = clientRef,
            Status = status,
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            Result = result,
            Error = error
        };
    }

    private async Task<Stream> DownloadFileAsync(string url, string label, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new InvalidOperationException($"{label} not found");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            response.Dispose();
            throw new InvalidOperationException($"{label} download failed: {response.StatusCode}, body: {body}");
        }

        // Copying detaches the returned stream from the disposed response.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        response.Dispose();
        buffer.Position = 0;
        return buffer;
    }

    private static OcrStatus ParseDocumentStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "queued" => OcrStatus.Pending,
            "processing" => OcrStatus.Processing,
            "completed" => OcrStatus.Completed,
            "failed" => OcrStatus.Failed,
            _ => throw new InvalidOperationException($"Unknown OCR document status '{status}'.")
        };

    private static OcrResult ParseResult(JsonElement resultElem)
    {
        return new OcrResult
        {
            Text = resultElem.GetProperty("text").GetString()!,
            PageCount = resultElem.GetProperty("pageCount").GetInt32(),
            DurationMs = resultElem.GetProperty("durationMs").GetInt64(),
            EngineVersion = resultElem.GetProperty("engineVersion").GetString()!,
            HasArchivedPdf = resultElem.GetProperty("hasArchivedPdf").GetBoolean(),
            ArchiveDownloadUrl = resultElem.TryGetProperty("archiveDownloadUrl", out var archiveUrl) && archiveUrl.ValueKind != JsonValueKind.Null
                ? archiveUrl.GetString()
                : null,
            HasThumbnail = resultElem.GetProperty("hasThumbnail").GetBoolean(),
            ThumbnailUrl = resultElem.TryGetProperty("thumbnailUrl", out var thumbUrl) && thumbUrl.ValueKind != JsonValueKind.Null
                ? thumbUrl.GetString()
                : null
        };
    }

    private static async Task<string> SafeReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private static Guid ExtractJobIdFromLocation(string location)
    {
        // Location: /api/v1/ocr/jobs/{jobId} or full URL
        var parts = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var jobIdStr = parts[^1];
        return Guid.Parse(jobIdStr);
    }
}
