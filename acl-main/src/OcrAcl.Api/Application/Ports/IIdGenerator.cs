namespace OcrAcl.Application.Ports;

/// <summary>
/// Identifier generation abstraction for `clientReference`, `documentId`, and correlation IDs.
/// Ensures deterministic output for tests and correct format for OCR contract.
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Generates a new `clientReference` for the OCR service.
    /// Format: `[a-z0-9_-]{1,64}` (OCR-SRC §3, §9).
    /// </summary>
    string GenerateClientReference();

    /// <summary>
    /// Generates a new `documentId` (UUID v4) for the ACL-owned document identity.
    /// </summary>
    Guid GenerateDocumentId();

    /// <summary>
    /// Generates a new correlation ID (UUID v4).
    /// </summary>
    Guid GenerateCorrelationId();
}

/// <summary>
/// Default implementation using <see cref="Guid.NewGuid"/> and a random suffix for clientReference.
/// </summary>
public sealed class DefaultIdGenerator : IIdGenerator
{
    private static readonly char[] ClientReferenceChars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
    private readonly Random _random = new();

    /// <inheritdoc />
    public string GenerateClientReference()
    {
        // Generate a prefix + random suffix to ensure uniqueness and format compliance
        // Format: doc-ref-xxxxxx (12 chars total, well within 64), lowercase only per OCR-SRC §3.1
        const string prefix = "doc-ref-";
        const int suffixLength = 6;
        char[] suffix = new char[suffixLength];
        for (int i = 0; i < suffixLength; i++)
        {
            suffix[i] = ClientReferenceChars[_random.Next(ClientReferenceChars.Length)];
        }
        return prefix + new string(suffix);
    }

    /// <inheritdoc />
    public Guid GenerateDocumentId() => Guid.NewGuid();

    /// <inheritdoc />
    public Guid GenerateCorrelationId() => Guid.NewGuid();
}

/// <summary>
/// Deterministic fake for testing. Produces predictable, incrementing IDs.
/// </summary>
public sealed class FakeIdGenerator : IIdGenerator
{
    private int _clientRefCounter = 0;
    private int _documentIdCounter = 0;
    private int _correlationIdCounter = 0;

    /// <inheritdoc />
    public string GenerateClientReference() => $"doc-ref-test{++_clientRefCounter:D6}";

    /// <inheritdoc />
    public Guid GenerateDocumentId() => new Guid(++_documentIdCounter, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <inheritdoc />
    public Guid GenerateCorrelationId() => new Guid(++_correlationIdCounter, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Resets all counters for test isolation.</summary>
    public void Reset()
    {
        _clientRefCounter = 0;
        _documentIdCounter = 0;
        _correlationIdCounter = 0;
    }
}