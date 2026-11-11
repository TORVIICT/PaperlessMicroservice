using Microsoft.AspNetCore.Mvc;
using Ocr.Api.Models;
using Ocr.Application.Services;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".gif", ".bmp", ".webp", ".heic", ".heif"
    };

    private readonly OcrJobService _jobService;

    public DocumentsController(OcrJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// POST /api/v1/documents - Subir un documento para procesamiento OCR
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] IFormFile? file,
        [FromForm] string? clientReference,
        [FromForm] string? language,
        [FromForm] string? mode,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiErrorResponse(
                Code: "INVALID_FILE",
                Message: "El archivo es requerido en form-data.",
                DocumentId: null,
                Details: new { }));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ApiErrorResponse(
                Code: "FILE_TOO_LARGE",
                Message: "El archivo excede el tamaño máximo permitido de 50 MB.",
                DocumentId: null,
                Details: new { sizeBytes = file.Length, maxBytes = MaxFileSizeBytes }));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new ApiErrorResponse(
                Code: "UNSUPPORTED_FILE_TYPE",
                Message: "El formato del archivo no está soportado.",
                DocumentId: null,
                Details: new { fileName = file.FileName, contentType = file.ContentType }));
        }

        byte[] fileBytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream, cancellationToken);
            fileBytes = stream.ToArray();
        }

        var job = await _jobService.CreateJobAsync(
            fileBytes: fileBytes,
            fileName: file.FileName,
            contentType: string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
            clientReference: clientReference ?? string.Empty,
            language: language ?? "eng",
            mode: mode ?? "skip",
            cancellationToken: cancellationToken);

        var locationUri = $"/api/v1/documents/{job.JobId}";
        Response.Headers.Append("Location", locationUri);

        var response = ToDocumentResponse(job);
        return Accepted(locationUri, response);
    }

    /// <summary>
    /// GET /api/v1/documents - Listar documentos paginados
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        JobStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!DocumentStatusMapper.TryParse(status, out var parsedStatus))
            {
                return BadRequest(new ApiErrorResponse(
                    Code: "INVALID_STATUS",
                    Message: $"Estado inválido. Valores permitidos: {string.Join(", ", DocumentStatusMapper.PublicStatuses)}.",
                    DocumentId: null,
                    Details: new { status }));
            }

            statusFilter = parsedStatus;
        }

        var (items, totalCount) = await _jobService.GetAllJobsAsync(
            page,
            pageSize,
            statusFilter,
            cancellationToken);
        var mappedItems = items.Select(ToDocumentResponse).ToList();

        return Ok(new PaginatedDocumentsResponse(
            Items: mappedItems,
            Total: totalCount,
            Page: page,
            PageSize: pageSize));
    }

    /// <summary>
    /// GET /api/v1/documents/{id} - Consultar datos y estado del documento
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Documento con id {id} no encontrado.",
                DocumentId: id,
                Details: new { }));
        }

        return Ok(ToDocumentResponse(job));
    }

    /// <summary>
    /// GET /api/v1/documents/{id}/text - Obtener texto extraído y metadatos
    /// </summary>
    [HttpGet("{id:guid}/text")]
    public async Task<IActionResult> GetDocumentText(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Documento con id {id} no encontrado.",
                DocumentId: id,
                Details: new { }));
        }

        if (job.Status == JobStatus.Failed)
        {
            return BadRequest(new ApiErrorResponse(
                Code: "OCR_PROCESSING_FAILED",
                Message: job.Error?.Message ?? "El procesamiento de OCR falló.",
                DocumentId: id,
                Details: new { error = job.Error }));
        }

        if (job.Status != JobStatus.Completed)
        {
            return StatusCode(StatusCodes.Status409Conflict, new ApiErrorResponse(
                Code: "DOCUMENT_NOT_READY",
                Message: "El procesamiento del documento aún no ha finalizado.",
                DocumentId: id,
                Details: new { status = DocumentStatusMapper.ToPublicStatus(job.Status) }));
        }

        return Ok(new DocumentTextResponse(
            DocumentId: job.JobId,
            Text: job.Result?.Text ?? string.Empty,
            PageCount: job.Result?.PageCount ?? 0,
            DurationMs: job.Result?.DurationMs ?? 0,
            EngineVersion: job.Result?.EngineVersion ?? string.Empty));
    }

    /// <summary>
    /// GET /api/v1/documents/{id}/original - Descargar archivo original
    /// </summary>
    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> GetOriginalDocument(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Documento con id {id} no encontrado.",
                DocumentId: id,
                Details: new { }));
        }

        if (string.IsNullOrEmpty(job.FileStoragePath) || !System.IO.File.Exists(job.FileStoragePath))
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Archivo original no encontrado en almacenamiento para id {id}.",
                DocumentId: id,
                Details: new { }));
        }

        return PhysicalFile(Path.GetFullPath(job.FileStoragePath), job.ContentType, job.FileName);
    }

    /// <summary>
    /// GET /api/v1/documents/{id}/archive - Descargar PDF buscable (PDF/A con OCR incrustado)
    /// </summary>
    [HttpGet("{id:guid}/archive")]
    public async Task<IActionResult> GetArchivedDocument(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Documento con id {id} no encontrado.",
                DocumentId: id,
                Details: new { }));
        }

        if (job.Status != JobStatus.Completed)
        {
            return StatusCode(StatusCodes.Status409Conflict, new ApiErrorResponse(
                Code: "DOCUMENT_NOT_READY",
                Message: "El PDF buscable aún no está disponible.",
                DocumentId: id,
                Details: new { status = DocumentStatusMapper.ToPublicStatus(job.Status) }));
        }

        if (job.Result == null || string.IsNullOrEmpty(job.Result.ArchivedPdfPath) || !System.IO.File.Exists(job.Result.ArchivedPdfPath))
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"PDF buscable no disponible para id {id}.",
                DocumentId: id,
                Details: new { }));
        }

        var downloadName = Path.GetFileNameWithoutExtension(job.FileName) + "_searchable.pdf";
        return PhysicalFile(Path.GetFullPath(job.Result.ArchivedPdfPath), "application/pdf", downloadName);
    }

    /// <summary>
    /// DELETE /api/v1/documents/{id} - Eliminar un documento y sus resultados
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _jobService.DeleteJobAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new ApiErrorResponse(
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Documento con id {id} no encontrado.",
                DocumentId: id,
                Details: new { }));
        }

        return NoContent();
    }

    private static DocumentResponse ToDocumentResponse(OcrJob job)
    {
        return new DocumentResponse(
            Id: job.JobId,
            FileName: job.FileName,
            ContentType: job.ContentType,
            SizeBytes: job.SizeBytes,
            Status: DocumentStatusMapper.ToPublicStatus(job.Status),
            CreatedAt: job.CreatedAt,
            CompletedAt: job.CompletedAt,
            PageCount: job.Result?.PageCount,
            Error: job.Error != null
                ? new ApiErrorResponse(job.Error.Code, job.Error.Message, job.JobId, new { })
                : null
        );
    }

}
