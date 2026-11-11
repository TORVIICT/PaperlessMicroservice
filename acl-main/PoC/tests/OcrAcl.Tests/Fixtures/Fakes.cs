using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Tests.Fixtures;

public sealed class RecordingOcrEngine : IOcrEngine
{
    public const string ResultText = "recorded text";
    public const int ResultPageCount = 2;
    public const double ResultConfidence = 0.95;

    private readonly string _engineDescriptor;

    public RecordingOcrEngine(string engineDescriptor = "FAKE-ENGINE")
    {
        _engineDescriptor = engineDescriptor;
    }

    public OcrDocument? LastDocument { get; private set; }

    public CancellationToken LastToken { get; private set; }

    public Task<OcrResult> RecognizeAsync(OcrDocument document, CancellationToken cancellationToken)
    {
        LastDocument = document;
        LastToken = cancellationToken;

        var result = new OcrResult(
            document.DocumentId,
            ResultText,
            ResultPageCount,
            document.Language,
            ResultConfidence,
            _engineDescriptor);

        return Task.FromResult(result);
    }
}

public sealed class FaultyOcrEngine : IOcrEngine
{
    public const string Marker = "INTERNAL-MARKER-7f3a";

    public Task<OcrResult> RecognizeAsync(OcrDocument document, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(Marker);
    }
}

public sealed class BlockingOcrEngine : IOcrEngine
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<OcrResult> RecognizeAsync(OcrDocument document, CancellationToken cancellationToken)
    {
        Started.TrySetResult();

        using var registration = cancellationToken.Register(
            () => CancellationObserved.TrySetResult());

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            CancellationObserved.TrySetResult();
            throw;
        }

        return new OcrResult(document.DocumentId, "unreachable", 1, document.Language, 0.5, "BLOCKING");
    }
}
