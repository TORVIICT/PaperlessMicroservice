namespace Ocr.Application.Ports;

/// <summary>Notifies the integration layer after an OCR result is durable.</summary>
public interface IOcrCompletionNotifier
{
    Task NotifyCompletedAsync(
        Guid documentId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
