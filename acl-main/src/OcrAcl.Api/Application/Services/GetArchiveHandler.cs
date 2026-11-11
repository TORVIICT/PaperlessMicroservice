namespace OcrAcl.Application.Services;

using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

/// <summary>
/// Use case: retrieve the OCR archive for a completed document.
/// Requests the archive stream through the OCR port and returns it.
/// </summary>
public sealed class GetArchiveHandler
{
    private readonly IOcrClient _ocrClient;
    private readonly IDocumentStore _documentStore;

    public GetArchiveHandler(IOcrClient ocrClient, IDocumentStore documentStore)
    {
        _ocrClient = ocrClient;
        _documentStore = documentStore;
    }

    /// <summary>
    /// Executes the get archive use case.
    /// </summary>
    /// <param name="documentId">ACL document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The archive stream, or null if document not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when document is not in completed state or has no archive.</exception>
    public async Task<Stream?> ExecuteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var record = await _documentStore.GetByDocumentIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        if (record.Status != OcrStatus.Completed)
        {
            throw new InvalidOperationException("Document is not completed.");
        }

        if (record.Result is null || !record.Result.HasArchivedPdf)
        {
            throw new InvalidOperationException("Archive not available for this document.");
        }

        return await _ocrClient.DownloadArchiveAsync(record.JobId, cancellationToken).ConfigureAwait(false);
    }
}