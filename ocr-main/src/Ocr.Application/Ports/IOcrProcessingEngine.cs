using Ocr.Domain.Entities;

namespace Ocr.Application.Ports;

public record OcrProcessingOptions(
    string Language = "eng",
    string Mode = "skip",
    string? OutputDirectory = null,
    Guid? JobId = null);

public interface IOcrProcessingEngine
{
    Task<OcrJobResult> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        string language,
        CancellationToken cancellationToken = default)
    {
        return ProcessAsync(fileBytes, fileName, new OcrProcessingOptions(Language: language), cancellationToken);
    }

    Task<OcrJobResult> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        OcrProcessingOptions options,
        CancellationToken cancellationToken = default);
}
