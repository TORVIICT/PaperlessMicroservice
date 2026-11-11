namespace OcrAcl.Infrastructure.Ocr;

using System.Net.Http;
using OcrAcl.Application.Ports;

/// <summary>
/// Delegating handler that adds <c>X-Correlation-ID</c> and <c>Authorization</c> headers
/// to every outbound OCR request (OCR-SRC §2.2, §9).
/// </summary>
public sealed class OcrOutboundHeadersHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly string _token;

    /// <summary>
    /// Creates a new outbound headers handler.
    /// </summary>
    /// <param name="correlationIdAccessor">Accessor for the correlation ID.</param>
    /// <param name="token">The bearer token to use (from configuration, per D-41).</param>
    public OcrOutboundHeadersHandler(ICorrelationIdAccessor correlationIdAccessor, string token)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _token = token;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add Authorization header
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        // Add X-Correlation-ID header
        var correlationId = _correlationIdAccessor.CorrelationId;
        request.Headers.Add("X-Correlation-ID", correlationId.ToString());

        // Add Accept header for JSON responses
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}