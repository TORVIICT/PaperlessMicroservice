namespace OcrAcl.Api.Translation;

using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;

/// <summary>
/// Translates the FE multipart upload request to the <see cref="SubmitDocumentHandler"/> input.
/// Validates presence, MIME type, size, and applies defaults for language/mode.
/// </summary>
public sealed class DocumentRequestTranslator
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    private readonly IIdGenerator _idGenerator;
    private readonly string _defaultLanguage;
    private readonly string _defaultMode;
    private readonly long _maxFileSizeBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRequestTranslator"/> class.
    /// </summary>
    /// <param name="idGenerator">Identifier generator for clientReference.</param>
    /// <param name="defaultLanguage">Default OCR language (ISO 639-2/3). Per D-37: "spa".</param>
    /// <param name="defaultMode">Default OCR mode (skip, redo, force). Per D-37: "skip".</param>
    /// <param name="maxFileSizeBytes">Maximum allowed file size in bytes. Per D-35: 50 MB.</param>
    public DocumentRequestTranslator(
        IIdGenerator idGenerator,
        string defaultLanguage = "spa",
        string defaultMode = "skip",
        long maxFileSizeBytes = 50 * 1024 * 1024)
    {
        _idGenerator = idGenerator;
        _defaultLanguage = defaultLanguage;
        _defaultMode = defaultMode;
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>
    /// Translates an uploaded file to the submit document input.
    /// </summary>
    /// <param name="file">The uploaded file from the multipart request.</param>
    /// <param name="language">Optional language override from request.</param>
    /// <param name="mode">Optional mode override from request.</param>
    /// <returns>The validated submit document input.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public SubmitDocumentInput Translate(IFormFile file, string? language = null, string? mode = null)
    {
        ValidateFile(file);

        var effectiveLanguage = string.IsNullOrWhiteSpace(language) ? _defaultLanguage : language.Trim();
        var effectiveMode = string.IsNullOrWhiteSpace(mode) ? _defaultMode : mode.Trim();

        return new SubmitDocumentInput(
            file.OpenReadStream(),
            file.FileName,
            file.ContentType,
            effectiveLanguage,
            effectiveMode);
    }

    /// <summary>
    /// Validates the uploaded file.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails with FE error code.</exception>
    private void ValidateFile(IFormFile file)
    {
        if (file is null)
        {
            throw new ArgumentException("File is required.", nameof(file));
        }

        if (file.Length == 0)
        {
            throw new ArgumentException("File content is empty.", nameof(file));
        }

        if (file.Length > _maxFileSizeBytes)
        {
            throw new ArgumentException($"File size {file.Length} bytes exceeds maximum of {_maxFileSizeBytes} bytes (50 MB).", nameof(file));
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"Unsupported file type: {file.ContentType}. Allowed: application/pdf, image/png, image/jpeg.", nameof(file));
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(file));
        }
    }
}