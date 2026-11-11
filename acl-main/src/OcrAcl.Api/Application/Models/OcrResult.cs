namespace OcrAcl.Application.Models;

/// <summary>
/// OCR job result payload when status is Completed.
/// Matches OCR-SRC §3.2 result object.
/// URLs are OCR-host-relative and never leave Application/Infrastructure (M-04, T-33).
/// </summary>
public sealed record OcrResult
{
    /// <summary>
    /// Extracted text content.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Number of pages detected.
    /// </summary>
    public required int PageCount { get; init; }

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// OCR engine version (e.g., "tesseract 5.3.0").
    /// </summary>
    public required string EngineVersion { get; init; }

    /// <summary>
    /// Whether a searchable PDF/A archive was generated.
    /// </summary>
    public required bool HasArchivedPdf { get; init; }

    /// <summary>
    /// OCR-host-relative download URL for the archive.
    /// Internal use only; never exposed to Gateway/Frontend.
    /// </summary>
    public string? ArchiveDownloadUrl { get; init; }

    /// <summary>
    /// Whether a thumbnail was generated.
    /// </summary>
    public required bool HasThumbnail { get; init; }

    /// <summary>
    /// OCR-host-relative download URL for the thumbnail.
    /// Internal use only; never exposed to Gateway/Frontend.
    /// </summary>
    public string? ThumbnailUrl { get; init; }
}