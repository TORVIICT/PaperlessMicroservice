using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Application.Services;

public sealed class OcrDocumentService
{
    private readonly IOcrEngine _engine;

    public OcrDocumentService(IOcrEngine engine)
    {
        _engine = engine;
    }

    public Task<OcrResult> ExecuteAsync(OcrDocument document, CancellationToken cancellationToken)
    {
        return _engine.RecognizeAsync(document, cancellationToken);
    }
}
