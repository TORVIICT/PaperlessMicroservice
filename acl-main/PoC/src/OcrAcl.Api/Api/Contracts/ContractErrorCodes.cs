using Microsoft.AspNetCore.Http;

namespace OcrAcl.Api.Contracts;

public static class ContractErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string InternalError = "internal_error";

    public const string InvalidRequestMessage = "The request is invalid.";
    public const string UnsupportedMediaTypeMessage = "Only application/json is supported.";
    public const string NotFoundMessage = "The requested resource was not found.";
    public const string MethodNotAllowedMessage = "The HTTP method is not allowed for this resource.";
    public const string InternalErrorMessage = "An unexpected error occurred.";
    public const string InvalidFieldMessage = "The value is invalid.";

    private static readonly IReadOnlyList<ContractErrorDetail> NoDetails =
        Array.Empty<ContractErrorDetail>();

    public static ContractErrorEnvelope Create(string code, string message, IReadOnlyList<ContractErrorDetail>? details = null)
    {
        return new ContractErrorEnvelope
        {
            Error = new ContractError
            {
                Code = code,
                Message = message,
                Details = details ?? NoDetails
            }
        };
    }

    public static (int StatusCode, ContractErrorEnvelope Body) ForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest =>
                (statusCode, Create(InvalidRequest, InvalidRequestMessage)),
            StatusCodes.Status415UnsupportedMediaType =>
                (statusCode, Create(UnsupportedMediaType, UnsupportedMediaTypeMessage)),
            StatusCodes.Status404NotFound =>
                (statusCode, Create(NotFound, NotFoundMessage)),
            StatusCodes.Status405MethodNotAllowed =>
                (statusCode, Create(MethodNotAllowed, MethodNotAllowedMessage)),
            StatusCodes.Status500InternalServerError =>
                (statusCode, Create(InternalError, InternalErrorMessage)),
            _ =>
                (StatusCodes.Status500InternalServerError, Create(InternalError, InternalErrorMessage))
        };
    }
}
