namespace OcrAcl.Application.Models;

/// <summary>
/// Internal error model used by the ACL.
/// Codes match FE-SRC §6 and D-42.
/// No HTTP status in Application layer (T-26).
/// </summary>
public sealed record AclError
{
    /// <summary>
    /// Error code (one of the defined FE error codes).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional additional details (e.g., original OCR error code, validation details).
    /// </summary>
    public object? Details { get; init; }
}

/// <summary>
/// Defined FE error codes per FE-SRC §6 and D-42.
/// </summary>
public static class AclErrorCodes
{
    /// <summary>The uploaded file is invalid or corrupted.</summary>
    public const string InvalidFile = "INVALID_FILE";

    /// <summary>The file type is not supported (not PDF, PNG, or JPEG).</summary>
    public const string UnsupportedFileType = "UNSUPPORTED_FILE_TYPE";

    /// <summary>The file exceeds the maximum allowed size (50 MB).</summary>
    public const string FileTooLarge = "FILE_TOO_LARGE";

    /// <summary>The requested document was not found.</summary>
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";

    /// <summary>The document is not ready (still processing or artifact not generated).</summary>
    public const string DocumentNotReady = "DOCUMENT_NOT_READY";

    /// <summary>The OCR processing failed.</summary>
    public const string OcrProcessingFailed = "OCR_PROCESSING_FAILED";

    /// <summary>The OCR service is unavailable.</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>All defined error codes.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        InvalidFile,
        UnsupportedFileType,
        FileTooLarge,
        DocumentNotFound,
        DocumentNotReady,
        OcrProcessingFailed,
        ServiceUnavailable
    };

    /// <summary>Checks if a code is a defined FE error code.</summary>
    public static bool IsDefined(string code) => All.Contains(code);
}