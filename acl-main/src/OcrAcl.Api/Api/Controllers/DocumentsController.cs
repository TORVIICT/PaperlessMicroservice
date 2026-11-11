namespace OcrAcl.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using OcrAcl.Api.Contracts;
using OcrAcl.Api.Translation;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;

/// <summary>
/// FE document endpoints (FE-E1, FE-E3, FE-E4, FE-E6).
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Produces("application/json")]
public sealed class DocumentsController : ControllerBase
{
    private readonly SubmitDocumentHandler _submitHandler;
    private readonly GetArchiveHandler _archiveHandler;
    private readonly IDocumentStore _documentStore;
    private readonly DocumentRequestTranslator _requestTranslator;
    private readonly DocumentResponseTranslator _responseTranslator;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IIdGenerator _idGenerator;
    private readonly IOcrPollingQueue _pollingQueue;

    public DocumentsController(
        SubmitDocumentHandler submitHandler,
        GetArchiveHandler archiveHandler,
        IDocumentStore documentStore,
        DocumentRequestTranslator requestTranslator,
        DocumentResponseTranslator responseTranslator,
        ICorrelationIdAccessor correlationIdAccessor,
        IIdGenerator idGenerator,
        IOcrPollingQueue pollingQueue)
    {
        _submitHandler = submitHandler;
        _archiveHandler = archiveHandler;
        _documentStore = documentStore;
        _requestTranslator = requestTranslator;
        _responseTranslator = responseTranslator;
        _correlationIdAccessor = correlationIdAccessor;
        _idGenerator = idGenerator;
        _pollingQueue = pollingQueue;
    }

    /// <summary>
    /// Upload a document for OCR processing (FE-E1).
    /// </summary>
    /// <param name="file">The document file (multipart/form-data, field name "file").</param>
    /// <param name="language">Optional OCR language override (default: spa).</param>
    /// <param name="mode">Optional OCR mode override (default: skip).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with the document model (initial status queued).</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FeDocumentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? language = null,
        [FromForm] string? mode = null,
        CancellationToken cancellationToken = default)
    {
        // Translate and validate request
        var input = _requestTranslator.Translate(file, language, mode);

        // Execute submit use case
        var result = await _submitHandler.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);

        // Create document record in store
        var documentId = _idGenerator.GenerateDocumentId();

        await _documentStore.CreateAsync(
            documentId,
            result.JobId,
            result.ClientReference,
            file.FileName,
            file.ContentType,
            file.Length,
            OcrStatus.Pending, // Initial status maps to FE "queued"
            result.CreatedAt,
            result.PollingUrl,
            cancellationToken).ConfigureAwait(false);

        // Enqueue for background polling (D-40: follow-up in background)
        await _pollingQueue.EnqueueAsync(result.JobId, result.ClientReference, documentId, cancellationToken).ConfigureAwait(false);

        // Get the created record and translate to FE response
        var record = await _documentStore.GetByDocumentIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        var response = _responseTranslator.Translate(record!);

        return AcceptedAtAction(nameof(GetById), new { id = documentId }, response);
    }

    /// <summary>
    /// Get document status and metadata (FE-E3).
    /// </summary>
    /// <param name="id">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document model.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FeDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _documentStore.GetByDocumentIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return NotFound(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotFound, $"Document {id} not found."),
                id));
        }

        var response = _responseTranslator.Translate(record);
        return Ok(response);
    }

    /// <summary>
    /// Get extracted text (FE-E4).
    /// </summary>
    /// <param name="id">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Text and page count.</returns>
    [HttpGet("{id:guid}/text")]
    [ProducesResponseType(typeof(FeDocumentTextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetText(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _documentStore.GetByDocumentIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return NotFound(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotFound, $"Document {id} not found."),
                id));
        }

        if (record.Status != OcrStatus.Completed || record.Result is null)
        {
            return Conflict(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotReady, "Document is not ready. OCR processing not completed."),
                id));
        }

        var response = new FeDocumentTextResponse
        {
            Id = record.DocumentId,
            Text = record.Result.Text,
            PageCount = record.Result.PageCount
        };

        return Ok(response);
    }

    /// <summary>
    /// Download searchable PDF/A archive (FE-E6).
    /// Streams the archive through the ACL; OCR host URL never exposed.
    /// </summary>
    /// <param name="id">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PDF/A archive stream.</returns>
    [HttpGet("{id:guid}/archive")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FeErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetArchive(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _documentStore.GetByDocumentIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return NotFound(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotFound, $"Document {id} not found."),
                id));
        }

        if (record.Status != OcrStatus.Completed || record.Result is null || !record.Result.HasArchivedPdf)
        {
            return Conflict(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotReady, "Archive not available. Document not completed or archive not generated."),
                id));
        }

        try
        {
            var stream = await _archiveHandler.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return NotFound(ErrorTranslator.Translate(
                    ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotFound, $"Archive for document {id} not found."),
                    id));
            }

            return File(stream, "application/pdf", $"{record.FileName}.pdf");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found or not yet generated", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(ErrorTranslator.Translate(
                ErrorTranslator.FromValidation(AclErrorCodes.DocumentNotReady, "Archive not available on OCR service."),
                id));
        }
    }
}