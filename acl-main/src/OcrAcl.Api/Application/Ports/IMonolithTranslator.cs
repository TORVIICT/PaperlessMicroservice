namespace OcrAcl.Application.Ports;

using OcrAcl.Application.Models;
using System.IO;

/// <summary>
/// Translates internal OCR results and document metadata to monolith ingestion payloads.
/// Implements the T-C flow (public REST API) per MON-API contract.
/// </summary>
public interface IMonolithTranslator
{
    /// <summary>
    /// Translates an <see cref="OcrResult"/> and ACL document metadata to the monolith upload request (Step 1).
    /// </summary>
    /// <param name="result">The OCR result.</param>
    /// <param name="originalFileName">The original file name from the upload.</param>
    /// <param name="originalContentType">The original MIME type from the upload.</param>
    /// <param name="fileContent">The file content to upload (PDF/A with text layer if available, else original).</param>
    /// <param name="title">Optional title override (from request or derived from filename).</param>
    /// <param name="created">Optional creation date from OCR result.</param>
    /// <returns>The monolith upload request data.</returns>
    MonolithUploadRequest TranslateToUploadRequest(
        OcrResult result,
        string originalFileName,
        string originalContentType,
        Stream fileContent,
        string? title = null,
        DateTimeOffset? created = null);

    /// <summary>
    /// Translates the OCR result text to the monolith content PATCH request (Step 3).
    /// </summary>
    /// <param name="result">The OCR result containing the extracted text.</param>
    /// <returns>The PATCH request body.</returns>
    MonolithContentPatchRequest TranslateToContentPatch(OcrResult result);

    /// <summary>
    /// Determines which file to upload based on whether the OCR service produced an archive PDF with text layer.
    /// </summary>
    /// <param name="result">The OCR result.</param>
    /// <param name="originalFileContent">The original file content.</param>
    /// <param name="originalFileName">The original file name.</param>
    /// <param name="originalContentType">The original MIME type.</param>
    /// <returns>The selected file to upload (original or PDF/A archive).</returns>
    UploadFileSelection SelectUploadFile(
        OcrResult result,
        Stream originalFileContent,
        string originalFileName,
        string originalContentType);
}

/// <summary>
/// Represents the selected file to upload (original or PDF/A archive).
/// </summary>
public sealed record UploadFileSelection(
    Stream FileContent,
    string FileName,
    string ContentType);

/// <summary>
/// Request data for the monolith upload (Step 1: POST /api/documents/post_document/).
/// </summary>
public sealed class MonolithUploadRequest
{
    /// <summary>The file content to upload (PDF/A with text layer when available, else original).</summary>
    public required Stream FileContent { get; init; }

    /// <summary>The original file name.</summary>
    public required string FileName { get; init; }

    /// <summary>The MIME type of the file (application/pdf, image/png, image/jpeg).</summary>
    public required string ContentType { get; init; }

    /// <summary>The document title (from request or derived from filename).</summary>
    public string? Title { get; init; }

    /// <summary>The document creation date (from OCR result if available, else derived).</summary>
    public DateTimeOffset? Created { get; init; }
}

/// <summary>
/// Request body for the content PATCH (Step 3: PATCH /api/documents/{id}/).
/// </summary>
public sealed class MonolithContentPatchRequest
{
    /// <summary>The extracted text from the OCR service (always sent per ACL Rule R3).</summary>
    public required string Content { get; init; }
}