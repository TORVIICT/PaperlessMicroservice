namespace OcrAcl.Application.Models;

public sealed record OcrDocument(
    string DocumentId,
    string FileName,
    string MimeType,
    byte[] Content,
    string Language);
