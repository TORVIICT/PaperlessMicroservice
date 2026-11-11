using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Application.Services;

/// <summary>
/// Input for the <see cref="SubmitDocumentHandler"/>.
/// </summary>
public sealed record SubmitDocumentInput(
    Stream FileContent,
    string FileName,
    string ContentType,
    string Language,
    string Mode);

/// <summary>
/// Result of the <see cref="SubmitDocumentHandler"/>.
/// </summary>
public sealed record SubmitDocumentResult(
    Guid JobId,
    string ClientReference,
    OcrStatus Status,
    DateTimeOffset CreatedAt,
    string PollingUrl);

/// <summary>
/// Use case: submit a document for OCR processing.
/// Validates input, generates clientReference, calls IOcrClient.SubmitAsync, returns created job state.
/// </summary>
public sealed class SubmitDocumentHandler
{
    private readonly IOcrClient _ocrClient;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public SubmitDocumentHandler(IOcrClient ocrClient, IIdGenerator idGenerator, IClock clock)
    {
        _ocrClient = ocrClient;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    /// <summary>
    /// Executes the submit document use case.
    /// </summary>
    /// <param name="input">Document input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created OCR job summary.</returns>
    /// <exception cref="ArgumentException">Thrown when input validation fails.</exception>
    public async Task<SubmitDocumentResult> ExecuteAsync(SubmitDocumentInput input, CancellationToken cancellationToken = default)
    {
        ValidateInput(input);

        var clientReference = _idGenerator.GenerateClientReference();
        var createdAt = _clock.UtcNow;

        var job = await _ocrClient.SubmitAsync(
            input.FileContent,
            input.FileName,
            input.ContentType,
            clientReference,
            input.Language,
            input.Mode,
            cancellationToken).ConfigureAwait(false);

        return new SubmitDocumentResult(job.JobId, job.ClientReference, job.Status, job.CreatedAt, job.PollingUrl!);
    }

    private static void ValidateInput(SubmitDocumentInput input)
    {
        if (input.FileContent is null || input.FileContent.Length == 0)
        {
            throw new ArgumentException("File content is required.", nameof(input.FileContent));
        }

        if (string.IsNullOrWhiteSpace(input.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(input.FileName));
        }

        var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/png",
            "image/jpeg"
        };

        if (!allowedMimeTypes.Contains(input.ContentType))
        {
            throw new ArgumentException($"Unsupported content type: {input.ContentType}. Allowed: application/pdf, image/png, image/jpeg", nameof(input.ContentType));
        }

        if (string.IsNullOrWhiteSpace(input.Language))
        {
            throw new ArgumentException("Language is required.", nameof(input.Language));
        }

        if (string.IsNullOrWhiteSpace(input.Mode))
        {
            throw new ArgumentException("Mode is required.", nameof(input.Mode));
        }
    }
}