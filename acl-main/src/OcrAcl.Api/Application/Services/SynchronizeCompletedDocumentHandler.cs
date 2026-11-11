using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Application.Services;

/// <summary>
/// Completes the synchronous OCR-to-Paperless hop requested by the webhook.
/// It owns no state: OCR is the source for files/results and Paperless owns the
/// legacy document after the delivery succeeds.
/// </summary>
public sealed class SynchronizeCompletedDocumentHandler
{
    private readonly IOcrClient _ocrClient;
    private readonly IMonolithClient _monolithClient;

    public SynchronizeCompletedDocumentHandler(IOcrClient ocrClient, IMonolithClient monolithClient)
    {
        _ocrClient = ocrClient;
        _monolithClient = monolithClient;
    }

    public async Task<int> ExecuteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _ocrClient.GetDocumentAsync(documentId, cancellationToken);
        if (document.Status != OcrStatus.Completed)
        {
            throw new InvalidOperationException($"OCR document {documentId} is not completed.");
        }

        var text = await _ocrClient.GetTextAsync(documentId, cancellationToken);

        Stream file;
        string fileName;
        string contentType;
        try
        {
            file = await _ocrClient.DownloadArchiveAsync(documentId, cancellationToken);
            fileName = $"{Path.GetFileNameWithoutExtension(document.FileName)}_searchable.pdf";
            contentType = "application/pdf";
        }
        catch (InvalidOperationException)
        {
            file = await _ocrClient.DownloadOriginalAsync(documentId, cancellationToken);
            fileName = document.FileName;
            contentType = document.ContentType;
        }

        await using (file)
        {
            var taskId = await _monolithClient.UploadDocumentAsync(
                file,
                fileName,
                contentType,
                Path.GetFileNameWithoutExtension(document.FileName),
                document.CreatedAt,
                cancellationToken);

            var result = await _monolithClient.PollTaskAsync(taskId, cancellationToken);
            if (result.Status != "success" || result.DocumentId is null)
            {
                throw new InvalidOperationException($"Paperless task {taskId} did not complete successfully.");
            }

            await _monolithClient.PatchContentAsync(result.DocumentId.Value, text.Text, cancellationToken);
            return result.DocumentId.Value;
        }
    }
}
