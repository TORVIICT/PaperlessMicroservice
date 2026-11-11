using Microsoft.AspNetCore.Mvc;
using Ocr.Application.Services;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Api.Controllers;

[ApiController]
[Route("api/v1/ocr")]
public class OcrJobsController : ControllerBase
{
    private readonly OcrJobService _jobService;

    public OcrJobsController(OcrJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    [HttpPost("jobs")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateJob(
        [FromForm] IFormFile? file,
        [FromForm] string? clientReference,
        [FromForm] string? language,
        [FromForm] string? mode,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "A valid file is required in form-data." });
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
            contentType: file.ContentType ?? "application/pdf",
            clientReference: clientReference ?? string.Empty,
            language: language ?? "eng",
            mode: mode ?? "skip",
            cancellationToken: cancellationToken);

        var locationUri = $"/api/v1/ocr/jobs/{job.JobId}";
        Response.Headers.Append("Location", locationUri);

        return Accepted(locationUri, new
        {
            jobId = job.JobId,
            clientReference = job.ClientReference,
            status = job.Status.ToString().ToLowerInvariant(),
            createdAt = job.CreatedAt
        });
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(jobId, cancellationToken);
        if (job == null)
        {
            return NotFound(new { error = $"Job with ID {jobId} not found." });
        }

        return Ok(new
        {
            jobId = job.JobId,
            clientReference = job.ClientReference,
            status = job.Status.ToString().ToLowerInvariant(),
            createdAt = job.CreatedAt,
            completedAt = job.CompletedAt,
            result = job.Result != null ? new
            {
                text = job.Result.Text,
                pageCount = job.Result.PageCount,
                durationMs = job.Result.DurationMs,
                engineVersion = job.Result.EngineVersion,
                hasArchivedPdf = !string.IsNullOrEmpty(job.Result.ArchivedPdfPath) && System.IO.File.Exists(job.Result.ArchivedPdfPath),
                archiveDownloadUrl = !string.IsNullOrEmpty(job.Result.ArchivedPdfPath) && System.IO.File.Exists(job.Result.ArchivedPdfPath)
                    ? $"/api/v1/ocr/jobs/{job.JobId}/archive"
                    : null,
                hasThumbnail = !string.IsNullOrEmpty(job.Result.ThumbnailPath) && System.IO.File.Exists(job.Result.ThumbnailPath),
                thumbnailUrl = !string.IsNullOrEmpty(job.Result.ThumbnailPath) && System.IO.File.Exists(job.Result.ThumbnailPath)
                    ? $"/api/v1/ocr/jobs/{job.JobId}/thumbnail"
                    : null
            } : null,
            error = job.Error != null ? new
            {
                code = job.Error.Code,
                message = job.Error.Message
            } : null
        });
    }

    [HttpGet("jobs/{jobId:guid}/archive")]
    public async Task<IActionResult> GetArchivedPdf(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(jobId, cancellationToken);
        if (job == null)
        {
            return NotFound(new { error = $"Job with ID {jobId} not found." });
        }

        if (job.Result == null || string.IsNullOrEmpty(job.Result.ArchivedPdfPath) || !System.IO.File.Exists(job.Result.ArchivedPdfPath))
        {
            return NotFound(new { error = $"Archived PDF for job {jobId} not found or not yet generated." });
        }

        var downloadName = Path.GetFileNameWithoutExtension(job.FileName) + "_archive.pdf";
        return PhysicalFile(Path.GetFullPath(job.Result.ArchivedPdfPath), "application/pdf", downloadName);
    }

    [HttpGet("jobs/{jobId:guid}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(jobId, cancellationToken);
        if (job == null)
        {
            return NotFound(new { error = $"Job with ID {jobId} not found." });
        }

        if (job.Result == null || string.IsNullOrEmpty(job.Result.ThumbnailPath) || !System.IO.File.Exists(job.Result.ThumbnailPath))
        {
            return NotFound(new { error = $"Thumbnail for job {jobId} not found or not yet generated." });
        }

        var ext = Path.GetExtension(job.Result.ThumbnailPath).ToLowerInvariant();
        var contentType = ext == ".webp" ? "image/webp" : "image/png";
        return PhysicalFile(Path.GetFullPath(job.Result.ThumbnailPath), contentType);
    }
}
