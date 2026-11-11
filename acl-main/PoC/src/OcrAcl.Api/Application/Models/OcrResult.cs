namespace OcrAcl.Application.Models;

public sealed record OcrResult(
    string DocumentId,
    string Text,
    int PageCount,
    string Language,
    double Confidence,
    string EngineDescriptor);
