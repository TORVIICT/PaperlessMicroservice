namespace OcrAcl.Api.Services;

using OcrAcl.Application.Ports;
using Microsoft.AspNetCore.Http;

/// <summary>
/// ASP.NET Core implementation of <see cref="ICorrelationIdAccessor"/>.
/// Reads inbound `X-Correlation-ID` header or generates a new UUID v4 if absent.
/// Policy: propagate inbound if valid, generate new if absent/invalid (proposed default for OQ-10).
/// </summary>
public sealed class HttpContextCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _correlationId;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpContextCorrelationIdAccessor"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP context accessor to read inbound headers.</param>
    public HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid CorrelationId
    {
        get
        {
            if (_correlationId.HasValue)
                return _correlationId.Value;

            var context = _httpContextAccessor.HttpContext;
            if (context?.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValues) == true)
            {
                var headerValue = headerValues.FirstOrDefault();
                if (Guid.TryParse(headerValue, out var parsed) && parsed != Guid.Empty)
                {
                    _correlationId = parsed;
                    return parsed;
                }
            }

            // Generate new UUID v4 if absent or invalid (proposed default for OQ-10)
            _correlationId = Guid.NewGuid();
            return _correlationId.Value;
        }
    }

    /// <inheritdoc />
    public void SetCorrelationId(Guid correlationId)
    {
        _correlationId = correlationId;
    }
}