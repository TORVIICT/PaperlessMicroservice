using System.IO;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Tests.Fixtures;

/// <summary>
/// Deterministic fake <see cref="IOcrClient"/> for unit tests.
/// Returns predictable job responses without any external call.
/// </summary>
public sealed class FakeOcrClient : IOcrClient
{
    private readonly Func<SubmitParams, OcrJob> _submitFactory;
    private readonly Func<Guid, OcrJob> _getJobFactory;
    private readonly Func<string, OcrJob> _getJobByUrlFactory;
    private readonly Func<Guid, Stream> _downloadArchiveFactory;
    private readonly Func<Guid, Stream> _downloadThumbnailFactory;

    internal sealed record SubmitParams(
        Stream FileContent,
        string FileName,
        string ContentType,
        string ClientReference,
        string Language,
        string Mode);

    /// <summary>
    /// Creates a fake client that returns a completed job for any submit.
    /// </summary>
    public static FakeOcrClient CompletedJob(Guid? jobId = null, string? clientReference = null) =>
        new(
            submitFactory: p => new OcrJob
            {
                JobId = jobId ?? Guid.NewGuid(),
                ClientReference = clientReference ?? p.ClientReference,
                Status = OcrStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
                Result = new OcrResult
                    {
                        Text = "fake text",
                        PageCount = 1,
                        DurationMs = 100,
                        EngineVersion = "FAKE-ENGINE",
                        HasArchivedPdf = true,
                        ArchiveDownloadUrl = "https://fake/archive.pdf",
                        HasThumbnail = true,
                        ThumbnailUrl = "https://fake/thumbnail.png"
                    },
                Error = null
            },
            getJobFactory: _ => throw new NotSupportedException("GetJobAsync not configured"),
            getJobByUrlFactory: _ => throw new NotSupportedException("GetJobByUrlAsync not configured"),
            downloadArchiveFactory: _ => new MemoryStream("fake archive"u8.ToArray()),
            downloadThumbnailFactory: _ => new MemoryStream("fake thumbnail"u8.ToArray()));

    /// <summary>
    /// Creates a fake client that returns a processing job for any submit.
    /// </summary>
    public static FakeOcrClient ProcessingJob(Guid? jobId = null, string? clientReference = null) =>
        new(
            submitFactory: p => new OcrJob
            {
                JobId = jobId ?? Guid.NewGuid(),
                ClientReference = clientReference ?? p.ClientReference,
                Status = OcrStatus.Processing,
                CreatedAt = DateTimeOffset.UtcNow,
                CompletedAt = null,
                Result = null,
                Error = null
            },
            getJobFactory: _ => throw new NotSupportedException("GetJobAsync not configured"),
            getJobByUrlFactory: _ => throw new NotSupportedException("GetJobByUrlAsync not configured"),
            downloadArchiveFactory: _ => throw new NotSupportedException("Archive not available for processing job"),
            downloadThumbnailFactory: _ => throw new NotSupportedException("Thumbnail not available for processing job"));

    /// <summary>
    /// Creates a fake client that returns a failed job for any submit.
    /// </summary>
    public static FakeOcrClient FailedJob(string errorCode = "ENGINE_ERROR", string errorMessage = "Fake engine error",
        Guid? jobId = null, string? clientReference = null) =>
        new(
            submitFactory: p => new OcrJob
            {
                JobId = jobId ?? Guid.NewGuid(),
                ClientReference = clientReference ?? p.ClientReference,
                Status = OcrStatus.Failed,
                CreatedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
                Result = null,
                Error = new OcrJobError { Code = errorCode, Message = errorMessage }
            },
            getJobFactory: _ => throw new NotSupportedException("GetJobAsync not configured"),
            getJobByUrlFactory: _ => throw new NotSupportedException("GetJobByUrlAsync not configured"),
            downloadArchiveFactory: _ => throw new NotSupportedException("Archive not available for failed job"),
            downloadThumbnailFactory: _ => throw new NotSupportedException("Thumbnail not available for failed job"));

    /// <summary>
    /// Creates a fake client with fully custom behavior.
    /// </summary>
    internal FakeOcrClient(
        Func<SubmitParams, OcrJob>? submitFactory = null,
        Func<Guid, OcrJob>? getJobFactory = null,
        Func<string, OcrJob>? getJobByUrlFactory = null,
        Func<Guid, Stream>? downloadArchiveFactory = null,
        Func<Guid, Stream>? downloadThumbnailFactory = null)
    {
        _submitFactory = submitFactory ?? (p => throw new NotSupportedException("SubmitAsync not configured"));
        _getJobFactory = getJobFactory ?? (_ => throw new NotSupportedException("GetJobAsync not configured"));
        _getJobByUrlFactory = getJobByUrlFactory ?? (_ => throw new NotSupportedException("GetJobByUrlAsync not configured"));
        _downloadArchiveFactory = downloadArchiveFactory ?? (_ => throw new NotSupportedException("DownloadArchiveAsync not configured"));
        _downloadThumbnailFactory = downloadThumbnailFactory ?? (_ => throw new NotSupportedException("DownloadThumbnailAsync not configured"));
    }

    public Task<OcrJob> SubmitAsync(Stream fileContent, string fileName, string contentType,
        string clientReference, string language, string mode, CancellationToken cancellationToken = default)
    {
        var param = new SubmitParams(fileContent, fileName, contentType, clientReference, language, mode);
        return Task.FromResult(_submitFactory(param));
    }

    public Task<OcrDocument> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("GetDocumentAsync not configured");

    public Task<OcrDocumentText> GetTextAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("GetTextAsync not configured");

    public Task<Stream> DownloadOriginalAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DownloadOriginalAsync not configured");

    public Task<OcrJob> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Task.FromResult(_getJobFactory(jobId));

    public Task<OcrJob> GetJobByUrlAsync(string pollingUrl, CancellationToken cancellationToken = default)
        => Task.FromResult(_getJobByUrlFactory(pollingUrl));

    public Task<Stream> DownloadArchiveAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Task.FromResult(_downloadArchiveFactory(jobId));

    public Task<Stream> DownloadThumbnailAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Task.FromResult(_downloadThumbnailFactory(jobId));
}
