namespace Ocr.Infrastructure.Notifications;

public sealed class OcrCompletionNotificationOptions
{
    public bool Enabled { get; init; }
    public string CompletionUrl { get; init; } = "http://localhost:5001/api/internal/v1/ocr/completed";
    public string InternalToken { get; init; } = string.Empty;
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
}
