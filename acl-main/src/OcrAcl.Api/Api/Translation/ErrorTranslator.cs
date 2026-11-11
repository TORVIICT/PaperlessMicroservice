namespace OcrAcl.Api.Translation;

using OcrAcl.Api.Contracts;
using OcrAcl.Application.Models;

/// <summary>
/// Translates internal <see cref="AclError"/> to the FE error envelope.
/// HTTP status mapping per D-42.
/// </summary>
public static class ErrorTranslator
{
    /// <summary>
    /// Translates an <see cref="AclError"/> to the FE error envelope.
    /// </summary>
    /// <param name="error">The internal ACL error.</param>
    /// <param name="documentId">Optional document ID (null for upload validation errors).</param>
    /// <returns>The FE error envelope.</returns>
    public static FeErrorEnvelope Translate(AclError error, Guid? documentId = null)
    {
        return new FeErrorEnvelope
        {
            Code = error.Code,
            Message = error.Message,
            DocumentId = documentId,
            Details = error.Details
        };
    }

    /// <summary>
    /// Gets the HTTP status code for a given FE error code.
    /// Per D-42.
    /// </summary>
    /// <param name="errorCode">FE error code.</param>
    /// <returns>HTTP status code.</returns>
    public static int GetHttpStatus(string errorCode)
    {
        return errorCode switch
        {
            AclErrorCodes.InvalidFile => 400,
            AclErrorCodes.UnsupportedFileType => 415,
            AclErrorCodes.FileTooLarge => 413,
            AclErrorCodes.DocumentNotFound => 404,
            AclErrorCodes.DocumentNotReady => 409,
            AclErrorCodes.OcrProcessingFailed => 422,
            AclErrorCodes.ServiceUnavailable => 503,
            _ => 500 // Should not happen for defined codes; fallback for safety
        };
    }

    /// <summary>
    /// Creates an <see cref="AclError"/> from a validation exception message and code.
    /// </summary>
    /// <param name="code">FE error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="details">Optional details.</param>
    /// <returns>The ACL error.</returns>
    public static AclError FromValidation(string code, string message, object? details = null)
    {
        if (!AclErrorCodes.IsDefined(code))
        {
            throw new ArgumentException($"Unknown error code: {code}", nameof(code));
        }

        return new AclError
        {
            Code = code,
            Message = message,
            Details = details
        };
    }
}