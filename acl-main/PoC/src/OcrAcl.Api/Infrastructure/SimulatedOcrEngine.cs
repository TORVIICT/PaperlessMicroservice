using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;

namespace OcrAcl.Infrastructure;

public sealed class SimulatedOcrEngine : IOcrEngine
{
    public const string EngineDescriptor = "SIMULATED-OCR";

    private const int BytesPerPage = 1024;
    private const double BaseConfidence = 0.50;
    private const double ConfidenceRange = 0.49;
    private const int WordsInText = 8;

    private static readonly string[] Vocabulary =
    {
        "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel",
        "india", "juliet", "kilo", "lima", "mike", "november", "oscar", "papa"
    };

    public async Task<OcrResult> RecognizeAsync(OcrDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var digest = ComputeDigest(document);
        var text = BuildText(digest);
        var pageCount = 1 + (document.Content.Length / BytesPerPage);
        var confidence = Math.Round(BaseConfidence + (digest[0] / 255.0 * ConfidenceRange), 2);

        var result = new OcrResult(
            document.DocumentId,
            text,
            pageCount,
            document.Language,
            confidence,
            EngineDescriptor);

        return await Task.FromResult(result).ConfigureAwait(false);
    }

    private static byte[] ComputeDigest(OcrDocument document)
    {
        var canonical = string.Join(
            "\n",
            document.DocumentId,
            document.FileName,
            document.MimeType,
            document.Language,
            Convert.ToBase64String(document.Content));

        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    private static string BuildText(byte[] digest)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < WordsInText; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            var wordIndex = digest[i] % Vocabulary.Length;
            builder.Append(Vocabulary[wordIndex]);
        }

        return builder.ToString();
    }
}
