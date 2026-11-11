using Microsoft.AspNetCore.Mvc;
using OcrAcl.Api.Contracts;
using OcrAcl.Application.Services;

namespace OcrAcl.Api.Controllers;

[ApiController]
[Route("api/internal/v1/ocr")]
public sealed class OcrCompletionController : ControllerBase
{
    private readonly SynchronizeCompletedDocumentHandler _handler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrCompletionController> _logger;

    public OcrCompletionController(
        SynchronizeCompletedDocumentHandler handler,
        IConfiguration configuration,
        ILogger<OcrCompletionController> logger)
    {
        _handler = handler;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("completed")]
    public async Task<IActionResult> Completed(
        [FromBody] OcrCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var configuredToken = _configuration["INTERNAL_SERVICE_TOKEN"];
        Request.Headers.TryGetValue("X-Internal-Token", out var providedToken);
        if (!string.IsNullOrWhiteSpace(configuredToken)
            && !string.Equals(configuredToken, providedToken.ToString(), StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (request.DocumentId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_DOCUMENT_ID" });
        }

        _logger.LogInformation(
            "Received OCR completion for {DocumentId} with correlation {CorrelationId}.",
            request.DocumentId,
            request.CorrelationId);

        var paperlessDocumentId = await _handler.ExecuteAsync(request.DocumentId, cancellationToken);
        return Ok(new { request.DocumentId, paperlessDocumentId });
    }
}
