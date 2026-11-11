namespace OcrAcl.Infrastructure.Monolith;

using System.Net.Http;
using OcrAcl.Application.Ports;

/// <summary>
/// Delegating handler that adds required outbound headers to monolith requests.
/// <para>
/// Per MON-API §2: <c>Authorization: Token <token></c> on every call.
/// Per OCR-SRC §2.2: <c>X-Correlation-ID</c> on every call.
/// </para>
/// </summary>
public sealed class MonolithOutboundHeadersHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly string _apiToken;

    public MonolithOutboundHeadersHandler(ICorrelationIdAccessor correlationIdAccessor, string apiToken)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _apiToken = apiToken;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add Authorization header (per MON-API §2)
        if (!string.IsNullOrEmpty(_apiToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", _apiToken);
        }

        // Add correlation ID header (per OCR-SRC §2.2)
        var correlationId = _correlationIdAccessor.CorrelationId;
        if (correlationId != Guid.Empty)
        {
            request.Headers.Add("X-Correlation-ID", correlationId.ToString());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}