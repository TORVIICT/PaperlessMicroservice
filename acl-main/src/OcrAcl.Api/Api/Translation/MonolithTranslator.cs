namespace OcrAcl.Api.Translation;

using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using System.IO;

public sealed class MonolithTranslator : IMonolithTranslator
{
    public MonolithUploadRequest TranslateToUploadRequest(
        OcrResult result,
        string originalFileName,
        string originalContentType,
        Stream fileContent,
        string? title = null,
        DateTimeOffset? created = null)
    {
        var effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : title;

        return new MonolithUploadRequest
        {
            FileName = originalFileName,
            ContentType = originalContentType,
            FileContent = fileContent,
            Title = effectiveTitle,
            Created = created
        };
    }

    public MonolithContentPatchRequest TranslateToContentPatch(OcrResult result)
    {
        return new MonolithContentPatchRequest
        {
            Content = result.Text ?? string.Empty
        };
    }

    public UploadFileSelection SelectUploadFile(
        OcrResult result,
        Stream originalFileContent,
        string originalFileName,
        string originalContentType)
    {
        if (result.HasArchivedPdf)
        {
            throw new InvalidOperationException(
                "HasArchivedPdf is true but archive content not provided. " +
                "Caller must fetch archive via IOcrClient.DownloadArchiveAsync and pass it as fileContent to TranslateToUploadRequest.");
        }

        originalFileContent.Position = 0;
        return new UploadFileSelection(originalFileContent, originalFileName, originalContentType);
    }
}