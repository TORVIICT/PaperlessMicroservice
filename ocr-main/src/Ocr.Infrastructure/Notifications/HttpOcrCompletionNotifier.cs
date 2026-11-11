using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Ocr.Application.Ports;

namespace Ocr.Infrastructure.Notifications;

public sealed class HttpOcrCompletionNotifier : IOcrCompletionNotifier
{
    private readonly HttpClient _httpClient;
    private readonly OcrCompletionNotificationOptions _options;
    private readonly ILogger<HttpOcrCompletionNotifier> _logger;

    public HttpOcrCompletionNotifier(
        HttpClient httpClient,
        OcrCompletionNotificationOptions options,
        ILogger<HttpOcrCompletionNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyCompletedAsync(
        Guid documentId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("ACL completion notification is disabled for document {DocumentId}.", documentId);
            return;
        }

        var attempts = Math.Max(1, _options.MaxAttempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.CompletionUrl)
                {
                    Content = JsonContent.Create(new { documentId, correlationId })
                };

                if (!string.IsNullOrWhiteSpace(_options.InternalToken))
                {
                    request.Headers.TryAddWithoutValidation("X-Internal-Token", _options.InternalToken);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation(
                    "ACL confirmed delivery for OCR document {DocumentId} on attempt {Attempt}.",
                    documentId,
                    attempt);
                return;
            }
            catch (Exception ex) when (attempt < attempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMilliseconds(
                    _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "ACL notification failed for document {DocumentId}; retrying in {Delay}.",
                    documentId,
                    delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The OCR result remains Completed. A failed integration must not
                // corrupt or repeat the expensive OCR processing itself.
                _logger.LogError(
                    ex,
                    "ACL notification exhausted {Attempts} attempts for document {DocumentId}.",
                    attempts,
                    documentId);
                return;
            }
        }
    }
}
