using OcrAcl.Application.Models;

namespace OcrAcl.Application.Ports;

public interface IOcrEngine
{
    Task<OcrResult> RecognizeAsync(OcrDocument document, CancellationToken cancellationToken);
}
