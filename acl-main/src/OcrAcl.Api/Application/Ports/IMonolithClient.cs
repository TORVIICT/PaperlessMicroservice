namespace OcrAcl.Application.Ports;

using System.IO;
using OcrAcl.Application.Models;

/// <summary>
/// Port for delivering OCR results to the monolith via its public REST API (MON-API, T-C flow).
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
public interface IMonolithClient
{
    /// <summary>
    /// Uploads a document to the monolith via <c>POST /api/documents/post_document/</c>.
    /// Per MON-API §3 Step 1: uploads file with optional title/created metadata.
    /// Per MON-API §2: requires Authorization header with Token.
    /// </summary>
    /// <param name="fileContent">The file content to upload (PDF/A with text layer when available, else original). Per MON-API §4: "PDF/A with text layer → Step 1, document".</param>
    /// <param name="fileName">The original file name. Per MON-API §3: field "document" in multipart.</param>
    /// <param name="contentType">The MIME type of the file. Per MON-API §3: "MIME validated by the monolith".</param>
    /// <param name="title">Optional document title. Per MON-API §3: "title" field (optional, max 127 chars).</param>
    /// <param name="created">Optional creation date. Per MON-API §4: "date (if OCR returns one) → Step 1, created".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task UUID string from the monolith. Per MON-API §3: body is JSON string with task UUID.</returns>
    Task<string> UploadDocumentAsync(
        Stream fileContent,
        string fileName,
        string contentType,
        string? title = null,
        DateTimeOffset? created = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the monolith task status until completion.
    /// Per MON-API §3 Step 2: <c>GET /api/tasks/?task_id=<uuid></c>.
    /// Per MON-API §5: "Polling: HTTP polling with back-off and an overall timeout, both configurable. An empty <c>results</c> list means the task is not registered yet: retry with back-off before failing."
    /// </summary>
    /// <param name="taskId">The task UUID from the upload response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task result containing the monolith document ID on success. Per MON-API §3: <c>results[0].result_data.document_id</c> when <c>status == "success"</c>.</returns>
    Task<MonolithTaskResult> PollTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches the document content via <c>PATCH /api/documents/{id}/</c>.
    /// Per MON-API §3 Step 3: <c>PATCH /api/documents/{id}/</c> with <c>{"content": "..."}</c>.
    /// Per MON-API §4: "text → Step 3, content (always sent)". ACL Rule R3: "Step 3 is always sent".
    /// Per MON-API §5: "Step 3 is always sent, so that <c>content</c> equals the OCR service's text".
    /// </summary>
    /// <param name="documentId">The monolith document ID from <c>result_data.document_id</c>.</param>
    /// <param name="content">The OCR extracted text. Per MON-API §4: "text → Step 3, content (always sent)".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PatchContentAsync(
        int documentId,
        string content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a monolith consume task.
/// Per MON-API §3 Step 2 response: <c>results[0]</c> with fields <c>task_id</c>, <c>status</c>, <c>result_data.document_id</c>, <c>related_document_ids</c>.
/// Per MON-API §3: status values observed: <c>pending</c>, <c>started</c>, <c>success</c>, <c>failure</c>, <c>revoked</c>.
/// </summary>
public sealed record MonolithTaskResult
{
    public required string TaskId { get; init; }
    public required string Status { get; init; } // pending, started, success, failure, revoked
    public DateTimeOffset? DateDone { get; init; }
    public double DurationSeconds { get; init; }
    public int? DocumentId { get; init; }
    public int[] RelatedDocumentIds { get; init; } = [];
    public int? DuplicateOf { get; init; }
    public bool DuplicateInTrash { get; init; }
}