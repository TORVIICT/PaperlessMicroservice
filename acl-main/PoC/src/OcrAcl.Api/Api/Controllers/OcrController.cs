using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OcrAcl.Api.Contracts;
using OcrAcl.Api.Translation;
using OcrAcl.Application.Services;

namespace OcrAcl.Api.Controllers;

[ApiController]
[Route("api/ocr")]
public sealed class OcrController : ControllerBase
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly OcrDocumentService _service;
    private readonly ILogger<OcrController> _logger;

    public OcrController(OcrDocumentService service, ILogger<OcrController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> PostDocument(CancellationToken cancellationToken)
    {
        var contentType = HttpContext.Request.ContentType;
        if (contentType is null ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, ContractErrorCodes.Create(
                ContractErrorCodes.UnsupportedMediaType, ContractErrorCodes.UnsupportedMediaTypeMessage));
        }

        OcrDocumentRequest? request;
        try
        {
            using var json = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: cancellationToken);
            request = json.RootElement.Deserialize<OcrDocumentRequest>(ReadOptions);
            if (request is not null && json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("language", out _))
            {
                request.LanguageSpecified = true;
            }
        }
        catch (JsonException exception)
        {
            return BadRequest(BuildDeserializationError(exception));
        }

        if (request is null)
        {
            return BadRequest(ContractErrorCodes.Create(
                ContractErrorCodes.InvalidRequest, ContractErrorCodes.InvalidRequestMessage));
        }

        if (!OcrRequestTranslator.TryTranslate(request, out var document, out var invalidFields))
        {
            var details = invalidFields
                .Select(field => new ContractErrorDetail
                {
                    Field = field,
                    Message = ContractErrorCodes.InvalidFieldMessage
                })
                .ToList();

            return BadRequest(ContractErrorCodes.Create(
                ContractErrorCodes.InvalidRequest, ContractErrorCodes.InvalidRequestMessage, details));
        }

        _logger.LogInformation("OCR request accepted for document {DocumentId}.", document!.DocumentId);

        var result = await _service.ExecuteAsync(document, cancellationToken);
        var response = OcrResponseTranslator.ToResponse(result);

        _logger.LogInformation("OCR simulation completed for document {DocumentId}.", document.DocumentId);

        return Ok(response);
    }

    [HttpGet("health")]
    [Produces("application/json")]
    public IActionResult GetHealth()
    {
        return Ok(new HealthResponse { Status = "ok" });
    }

    private static ContractErrorEnvelope BuildDeserializationError(JsonException exception)
    {
        var field = NormalizeJsonPath(exception.Path);
        if (field is null)
        {
            return ContractErrorCodes.Create(
                ContractErrorCodes.InvalidRequest, ContractErrorCodes.InvalidRequestMessage);
        }

        var details = new[]
        {
            new ContractErrorDetail
            {
                Field = field,
                Message = ContractErrorCodes.InvalidFieldMessage
            }
        };

        return ContractErrorCodes.Create(
            ContractErrorCodes.InvalidRequest, ContractErrorCodes.InvalidRequestMessage, details);
    }

    private static string? NormalizeJsonPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var last = path.TrimStart('$');
        var lastDot = last.LastIndexOf('.');
        var name = lastDot >= 0 ? last[(lastDot + 1)..] : last;
        name = name.Trim('[', ']', '"', '\'');

        return name.Length == 0 ? null : name;
    }
}
