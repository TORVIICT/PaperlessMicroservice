using System.Text.RegularExpressions;
using OcrAcl.Api.Contracts;
using OcrAcl.Application.Models;

namespace OcrAcl.Api.Translation;

public static class OcrRequestTranslator
{
    private const string DefaultLanguage = "eng";
    private const int DocumentIdMaxLength = 64;
    private const int FileNameMaxLength = 255;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.Ordinal)
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    private static readonly HashSet<string> AllowedLanguages = new(StringComparer.Ordinal)
    {
        "eng",
        "spa"
    };

    private static readonly Regex Base64Pattern = new(
        @"^[A-Za-z0-9+/]*={0,2}$",
        RegexOptions.CultureInvariant);

    public static bool TryTranslate(
        OcrDocumentRequest request,
        out OcrDocument? document,
        out IReadOnlyList<string> invalidFields)
    {
        var invalid = new List<string>();

        var documentId = ValidateText(request.DocumentId, "documentId", DocumentIdMaxLength, invalid);
        var fileName = ValidateText(request.FileName, "fileName", FileNameMaxLength, invalid);
        var mimeType = ValidateMimeType(request.MimeType, invalid);
        var content = ValidateContent(request.ContentBase64, invalid);
        var language = ValidateLanguage(request.Language, request.LanguageSpecified, invalid);

        invalidFields = invalid;
        if (invalid.Count > 0 || documentId is null || fileName is null || mimeType is null ||
            content is null || language is null)
        {
            document = null;
            return false;
        }

        document = new OcrDocument(documentId, fileName, mimeType, content, language);
        return true;
    }

    private static string? ValidateText(string? value, string field, int maxLength, List<string> invalid)
    {
        if (value is null || value.Length == 0)
        {
            invalid.Add(field);
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            invalid.Add(field);
            return null;
        }

        return trimmed;
    }

    private static string? ValidateMimeType(string? value, List<string> invalid)
    {
        if (value is null || !AllowedMimeTypes.Contains(value))
        {
            invalid.Add("mimeType");
            return null;
        }

        return value;
    }

    private static string? ValidateLanguage(string? value, bool specified, List<string> invalid)
    {
        if (!specified)
        {
            return DefaultLanguage;
        }

        if (value is null || !AllowedLanguages.Contains(value))
        {
            invalid.Add("language");
            return null;
        }

        return value;
    }

    private static byte[]? ValidateContent(string? value, List<string> invalid)
    {
        if (value is null || value.Length == 0 || value.Length % 4 != 0 || !Base64Pattern.IsMatch(value))
        {
            invalid.Add("contentBase64");
            return null;
        }

        if (!TryDecodeBase64(value, out var bytes))
        {
            invalid.Add("contentBase64");
            return null;
        }

        return bytes;
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
