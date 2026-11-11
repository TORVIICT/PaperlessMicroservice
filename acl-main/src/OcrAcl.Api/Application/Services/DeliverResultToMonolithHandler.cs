using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Application.Services;

/// <summary>
/// Orchestrates the delivery of completed OCR job results to the monolith via the public REST API.
/// <para>
/// Sources: MON-API §3 (Flow), §4 (Mapping), §5 (ACL Rules); D-44.
/// </para>
/// </summary>
public sealed class DeliverResultToMonolithHandler
{
    private readonly IOcrClient _ocrClient;
    private readonly IMonolithClient _monolithClient;
    private readonly IDocumentStore _documentStore;
    private readonly IClock _clock;
    private readonly IMonolithTranslator _translator;

    public DeliverResultToMonolithHandler(
        IOcrClient ocrClient,
        IMonolithClient monolithClient,
        IDocumentStore documentStore,
        IClock clock,
        IMonolithTranslator translator)
    {
        _ocrClient = ocrClient;
        _monolithClient = monolithClient;
        _documentStore = documentStore;
        _clock = clock;
        _translator = translator;
    }

    /// <summary>
    /// Delivers a completed OCR job result to the monolith via the public REST API.
    /// Called by the background polling service when an OCR job completes.
    /// Implements the T-C flow per MON-API:
    /// Step 1: <c>POST /api/documents/post_document/</c> (MON-API §3)
    /// Step 2: <c>GET /api/tasks/?task_id=...</c> polling (MON-API §3, §5)
    /// Step 3: <c>PATCH /api/documents/{id}/</c> with content (MON-API §3, §4, §5)
    /// </summary>
    /// <param name="documentId">The ACL document ID.</param>
    /// <param name="completedJob">The completed OCR job with result.</param>
    /// <param name="originalFileContent">The original file content to upload.</param>
    /// <param name="originalFileName">The original file name.</param>
    /// <param name="originalContentType">The original MIME type.</param>
    /// <param name="title">Optional title from the original request.</param>
    /// <param name="created">Optional creation date from the original request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The monolith document ID.</returns>
    public async Task<int> DeliverCompletedJobAsync(
        Guid documentId,
        OcrJob completedJob,
        Stream originalFileContent,
        string originalFileName,
        string originalContentType,
        string? title = null,
        DateTimeOffset? created = null,
        CancellationToken cancellationToken = default)
    {
        // Per MON-API §5 ACL Rule R1: "Upload PDF/A with text layer when OCR service delivers one; else original file"
        // Per MON-API §4: "PDF/A with text layer → Step 1, document" when OCR produces one
        var uploadFile = _translator.SelectUploadFile(completedJob.Result!, originalFileContent, originalFileName, originalContentType);
        
        // Step 1: Upload to monolith (MON-API §3 Step 1: POST /api/documents/post_document/)
        // Per MON-API §4: title → Step 1; date → Step 1; file → document
        var uploadRequest = _translator.TranslateToUploadRequest(
            completedJob.Result!,
            uploadFile.FileName,
            uploadFile.ContentType,
            uploadFile.FileContent,
            title,
            created);

        var taskId = await _monolithClient.UploadDocumentAsync(
            uploadRequest.FileContent,
            uploadRequest.FileName,
            uploadRequest.ContentType,
            uploadRequest.Title,
            uploadRequest.Created,
            cancellationToken).ConfigureAwait(false);

        // Step 2: Poll task until completion (MON-API §3 Step 2, §5)
        // Per MON-API §5: "Polling: HTTP polling with back-off and an overall timeout, both configurable.
        // An empty <c>results</c> list means the task is not registered yet: retry with back-off before failing."
        var taskResult = await _monolithClient.PollTaskAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (taskResult.Status != "success" || taskResult.DocumentId == null)
        {
            throw new InvalidOperationException($"Monolith task {taskResult.TaskId} failed or returned no document ID. Status: {taskResult.Status}");
        }

        var monolithDocumentId = taskResult.DocumentId.Value;

        // Step 3: PATCH content with OCR text
        // Per MON-API §3 Step 3: PATCH /api/documents/{id}/ with {"content": "..."}
        // Per MON-API §4: "text → Step 3, content (always sent)"
        // Per MON-API §5 ACL Rule R3: "Step 3 is always sent, so that <c>content</c> equals the OCR service's text"
        // Per D-44 proposed default: always send PATCH content, so content equals OCR service's text
        var patchRequest = _translator.TranslateToContentPatch(completedJob.Result!);
        
        await _monolithClient.PatchContentAsync(monolithDocumentId, patchRequest.Content, cancellationToken).ConfigureAwait(false);

        // Update ACL store with monolith document ID for idempotency (D-34, MON-API §5 R4)
        // Per MON-API §5: "At most one upload per job: the ACL stores <c>documentId</c> ↔ monolith <c>document_id</c> per job"
        await _documentStore.UpdateMonolithDocumentIdAsync(documentId, monolithDocumentId, cancellationToken).ConfigureAwait(false);

        return monolithDocumentId;
    }
}