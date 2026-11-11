using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OcrAcl.Tests.Fixtures;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> for testing <see cref="OcrAcl.Infrastructure.Ocr.HttpOcrClient"/>.
/// Simulates the OCR service contract (OCR-SRC §3–§4) without any external calls.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<CapturedRequest> _requests = new();

    /// <summary>
    /// Gets all requests that were sent through this handler.
    /// </summary>
    public IReadOnlyList<CapturedRequest> Requests => _requests;

    /// <summary>
    /// Queues a response to be returned for the next request.
    /// </summary>
    public void QueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

    /// <summary>
    /// Queues a 202 Accepted response with Location header and job body.
    /// </summary>
    public void QueueSubmitResponse(Guid jobId, string clientReference, string? location = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            jobId = jobId.ToString().ToLowerInvariant(),
            clientReference,
            status = "processing",
            createdAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        });

        var response = new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        response.Headers.Location = new Uri(location ?? $"/api/v1/ocr/jobs/{jobId}", UriKind.Relative);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a 200 OK job status response.
    /// </summary>
    public void QueueJobStatusResponse(Guid jobId, string clientReference, string status, string? resultJson = null, string? errorJson = null, DateTimeOffset? completedAt = null)
    {
        var bodyObj = new
        {
            jobId = jobId.ToString().ToLowerInvariant(),
            clientReference,
            status,
            createdAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            completedAt = completedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            result = resultJson != null ? JsonDocument.Parse(resultJson).RootElement : (object?)null,
            error = errorJson != null ? JsonDocument.Parse(errorJson).RootElement : (object?)null
        };

        var body = JsonSerializer.Serialize(bodyObj, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a 200 OK job status response with processing status.
    /// </summary>
    public void QueueProcessingResponse(Guid jobId, string clientReference) =>
        QueueJobStatusResponse(jobId, clientReference, "processing");

    /// <summary>
    /// Queues a 200 OK job status response with completed status.
    /// </summary>
    public void QueueCompletedResponse(Guid jobId, string clientReference, string text, int pageCount, long durationMs, string engineVersion, bool hasArchive, bool hasThumbnail) =>
        QueueJobStatusResponse(jobId, clientReference, "completed",
            resultJson: JsonSerializer.Serialize(new
            {
                text,
                pageCount,
                durationMs,
                engineVersion,
                hasArchivedPdf = hasArchive,
                archiveDownloadUrl = hasArchive ? "/api/v1/ocr/jobs/" + jobId + "/archive" : null,
                hasThumbnail,
                thumbnailUrl = hasThumbnail ? "/api/v1/ocr/jobs/" + jobId + "/thumbnail" : null
            }));

    /// <summary>
    /// Queues a 200 OK job status response with failed status.
    /// </summary>
    public void QueueFailedResponse(Guid jobId, string clientReference, string errorCode, string errorMessage) =>
        QueueJobStatusResponse(jobId, clientReference, "failed", errorJson: JsonSerializer.Serialize(new { code = errorCode, message = errorMessage }));

    /// <summary>
    /// Queues a 404 Not Found response.
    /// </summary>
    public void QueueNotFoundResponse(string errorMessage = "Job not found")
    {
        var body = JsonSerializer.Serialize(new { error = errorMessage });
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a 400 Bad Request response (RFC 9457).
    /// </summary>
    public void QueueBadRequestResponse(string detail = "Invalid request")
    {
        var body = JsonSerializer.Serialize(new { type = "about:blank", title = "Bad Request", status = 400, detail, instance = "/api/v1/ocr/jobs" });
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a 415 Unsupported Media Type response.
    /// </summary>
    public void QueueUnsupportedMediaTypeResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a 503 Service Unavailable response (transient failure).
    /// </summary>
    public void QueueServiceUnavailableResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues an archive download response.
    /// </summary>
    public void QueueArchiveResponse(byte[] content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a thumbnail download response.
    /// </summary>
    public void QueueThumbnailResponse(byte[] content, string contentType = "image/png")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        _responses.Enqueue(response);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Capture request details before sending
        string? contentString = null;
        Dictionary<string, List<string>> contentHeaders = new();
        if (request.Content != null)
        {
            contentString = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            foreach (var header in request.Content.Headers)
            {
                contentHeaders[header.Key] = header.Value.ToList();
            }
        }

        var captured = new CapturedRequest
        {
            Method = request.Method,
            RequestUri = request.RequestUri,
            Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToList()),
            ContentHeaders = contentHeaders,
            Content = contentString,
            CancellationToken = cancellationToken
        };
        _requests.Add(captured);

        // Check for cancellation before responding
        cancellationToken.ThrowIfCancellationRequested();

        if (_responses.Count > 0)
        {
            return _responses.Dequeue();
        }

        // Default: 404
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Represents a captured HTTP request for test assertions.
    /// </summary>
    public sealed class CapturedRequest
    {
        public HttpMethod Method { get; init; } = default!;
        public Uri? RequestUri { get; init; }
        public Dictionary<string, List<string>> Headers { get; init; } = new();
        public Dictionary<string, List<string>> ContentHeaders { get; init; } = new();
        public string? Content { get; init; }
        public CancellationToken CancellationToken { get; init; }
    }
}