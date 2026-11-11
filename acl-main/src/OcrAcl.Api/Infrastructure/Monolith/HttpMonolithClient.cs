namespace OcrAcl.Infrastructure.Monolith;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Infrastructure.Monolith.Dtos;
using OcrAcl.Infrastructure.Ocr;

/// <summary>
/// HTTP implementation of <see cref="IMonolithClient"/> using the monolith's public REST API (MON-API, T-C flow).
/// <para>
/// Sources:
/// <list type="bullet">
///   <item><description>MON-API §2 (Authentication): Authorization header required on every call</description></item>
///   <item><description>MON-API §3 (Flow): Three-step flow (POST upload → GET task polling → PATCH content)</description></item>
///   <item><description>MON-API §4 (Mapping): OCR result → monolith fields mapping</description></item>
///   <item><description>MON-API §5 (ACL Rules): File selection, ordering, idempotency, polling, failure handling</description></item>
///   <item><description>D-44 (OQ-01 resolved via T-C public REST API)</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class HttpMonolithClient : IMonolithClient
{
    private readonly HttpClient _httpClient;
    private readonly MonolithOptions _options;
    private readonly PollingPolicy _pollingPolicy;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpMonolithClient(HttpClient httpClient, MonolithOptions options, PollingPolicy pollingPolicy)
    {
        _httpClient = httpClient;
        _options = options;
        _pollingPolicy = pollingPolicy;
    }

    /// <summary>
    /// Uploads a document to the monolith via <c>POST /api/documents/post_document/</c>.
    /// Per MON-API §3 Step 1: uploads file with optional title/created metadata.
    /// Per MON-API §2: requires Authorization header with Token.
    /// Per MON-API §3: field "document" (file), "title" (optional), "created" (optional), "from_webui" (false for ACL).
    /// </summary>
    public async Task<string> UploadDocumentAsync(
        Stream fileContent,
        string fileName,
        string contentType,
        string? title = null,
        DateTimeOffset? created = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents/post_document/");
        
        var form = new MultipartFormDataContent();
        
        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        // Per MON-API §3: field "document" in multipart
        form.Add(fileStreamContent, "document", fileName);
        
        if (!string.IsNullOrWhiteSpace(title))
        {
            // Per MON-API §3: optional "title" field (max 127 chars)
            form.Add(new StringContent(title), "title");
        }
        
        if (created.HasValue)
        {
            // Per MON-API §4: "date (if OCR returns one) → Step 1, created"
            form.Add(new StringContent(created.Value.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")), "created");
        }
        
        // Per MON-API §3: "from_webui" field; false for ACL service
        form.Add(new StringContent("false"), "from_webui");
        
        request.Content = form;

        // Per MON-API §2: Authorization header required on every call
        // Handled by MonolithOutboundHeadersHandler
        
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var taskId = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // Per MON-API §3 Step 1: body is a JSON string with the task UUID
            return taskId.Trim('"');
        }

        var errorBody = await SafeReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException($"Upload failed: {response.StatusCode}, body: {errorBody}");
    }

    /// <summary>
    /// Polls the monolith task status until completion.
    /// Per MON-API §3 Step 2: <c>GET /api/tasks/?task_id=<uuid></c>.
    /// Per MON-API §5: "Polling: HTTP polling with back-off and an overall timeout, both configurable. An empty <c>results</c> list means the task is not registered yet: retry with back-off before failing."
    /// Per MON-API §3: status values observed: <c>pending</c>, <c>started</c>, <c>success</c>, <c>failure</c>, <c>revoked</c>.
    /// </summary>
    public async Task<MonolithTaskResult> PollTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/tasks/?task_id={Uri.EscapeDataString(taskId)}";
        
        // Per MON-API §5: "Initial wait after upload before first poll" (500 ms)
        await _pollingPolicy.WaitInitialAsync(cancellationToken).ConfigureAwait(false);
        
        var attempt = 0;
        var timeoutCts = new CancellationTokenSource(PollingPolicy.GlobalTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        while (true)
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            
            var response = await _httpClient.GetAsync($"/api/tasks/?task_id={Uri.EscapeDataString(taskId)}", linkedCts.Token).ConfigureAwait(false);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Task {taskId} not found");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadResponseBodyAsync(response, linkedCts.Token).ConfigureAwait(false);
                throw new InvalidOperationException($"Task polling failed: {response.StatusCode}, body: {body}");
            }
            
            var json = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<MonolithTasksResponse>(json, _jsonOptions);
            
            if (result?.Results == null || result.Results.Count == 0)
            {
                // Per MON-API §5: "An empty <c>results</c> list means the task is not registered yet: retry with back-off before failing."
                await _pollingPolicy.WaitPollDelayAsync(++attempt, linkedCts.Token).ConfigureAwait(false);
                continue;
            }
            
            var task = result.Results[0];
            
            // Per MON-API §3: status values observed: "pending", "started", "success", "failure", "revoked"
            if (task.Status == "success")
            {
                // Per MON-API §3: <c>results[0].result_data.document_id</c> when <c>status == "success"</c>
                return new MonolithTaskResult
                {
                    TaskId = task.TaskId,
                    Status = task.Status,
                    DateDone = task.DateDone,
                    DurationSeconds = task.DurationSeconds ?? 0,
                    DocumentId = task.ResultData?.DocumentId,
                    RelatedDocumentIds = task.RelatedDocumentIds?.ToArray() ?? [],
                    DuplicateOf = task.ResultData?.DuplicateOf,
                    DuplicateInTrash = task.ResultData?.DuplicateInTrash ?? false
                };
            }
            
            if (task.Status == "failure")
            {
                // Per MON-API §5: "Failure: <c>status == \"failure\"</c> (or timeout, or HTTP error) → the delivery is marked failed with an explicit error"
                var errorBody = task.ResultData?.Error ?? "Unknown error";
                throw new InvalidOperationException($"Monolith task {taskId} failed: {errorBody}");
            }
            
            // Per MON-API §3: status values observed: "pending", "started" (non-terminal)
            // Per MON-API §5: "Polling: HTTP polling with back-off and an overall timeout... retry with back-off before failing"
            await _pollingPolicy.WaitPollDelayAsync(++attempt, linkedCts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Patches the document content via <c>PATCH /api/documents/{id}/</c>.
    /// Per MON-API §3 Step 3: <c>PATCH /api/documents/{id}/</c> with <c>{"content": "..."}</c>.
    /// Per MON-API §4: "text → Step 3, content (always sent)".
    /// Per MON-API §5 ACL Rule R3: "Step 3 is always sent, so that <c>content</c> equals the OCR service's text".
    /// </summary>
    public async Task PatchContentAsync(
        int documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/documents/{documentId}/";
        
        // Use custom JsonContent to set Content-Type to exactly "application/json" 
        // (monolith rejects "application/json; charset=utf-8")
        var json = JsonSerializer.Serialize(new { content });
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Content patch failed for document {documentId}: {response.StatusCode}, body: {body}");
        }
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
}
