namespace OcrAcl.Infrastructure.Store;

using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using System.Collections.Concurrent;

/// <summary>
/// In-memory implementation of <see cref="IDocumentStore"/>.
/// Per D-34: ACL-owned in-memory store; state is lost on restart.
/// </summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<Guid, DocumentRecord> _byDocumentId = new();
    private readonly ConcurrentDictionary<Guid, Guid> _jobIdToDocumentId = new();
    private readonly ConcurrentDictionary<string, Guid> _clientReferenceToDocumentId = new();

    public Task CreateAsync(
        Guid documentId,
        Guid jobId,
        string clientReference,
        string fileName,
        string contentType,
        long sizeBytes,
        OcrStatus initialStatus,
        DateTimeOffset createdAt,
        string pollingUrl,
        CancellationToken cancellationToken = default)
    {
        var record = new DocumentRecord
        {
            DocumentId = documentId,
            JobId = jobId,
            ClientReference = clientReference,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Status = initialStatus,
            CreatedAt = createdAt,
            CompletedAt = null,
            Result = null,
            Error = null,
            PollingUrl = pollingUrl,
            MonolithDocumentId = null
        };

        _byDocumentId[documentId] = record;
        _jobIdToDocumentId[jobId] = documentId;
        _clientReferenceToDocumentId[clientReference] = documentId;

        return Task.CompletedTask;
    }

    public Task<DocumentRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _byDocumentId.TryGetValue(documentId, out var record);
        return Task.FromResult(record);
    }

    public Task<DocumentRecord?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobIdToDocumentId.TryGetValue(jobId, out var documentId))
        {
            _byDocumentId.TryGetValue(documentId, out var record);
            return Task.FromResult(record);
        }
        return Task.FromResult<DocumentRecord?>(null);
    }

    public Task<DocumentRecord?> GetByClientReferenceAsync(string clientReference, CancellationToken cancellationToken = default)
    {
        if (_clientReferenceToDocumentId.TryGetValue(clientReference, out var documentId))
        {
            _byDocumentId.TryGetValue(documentId, out var record);
            return Task.FromResult(record);
        }
        return Task.FromResult<DocumentRecord?>(null);
    }

    public Task<bool> UpdateStatusAsync(Guid documentId, OcrStatus status, DateTimeOffset? completedAt = null, CancellationToken cancellationToken = default)
    {
        if (!_byDocumentId.TryGetValue(documentId, out var record))
        {
            return Task.FromResult(false);
        }

        var updated = record with
        {
            Status = status,
            CompletedAt = completedAt ?? (status.IsTerminal() ? DateTimeOffset.UtcNow : null)
        };

        _byDocumentId[documentId] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateResultAsync(Guid documentId, OcrResult result, DateTimeOffset completedAt, CancellationToken cancellationToken = default)
    {
        if (!_byDocumentId.TryGetValue(documentId, out var record))
        {
            return Task.FromResult(false);
        }

        var updated = record with
        {
            Status = OcrStatus.Completed,
            CompletedAt = completedAt,
            Result = result
        };

        _byDocumentId[documentId] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateErrorAsync(Guid documentId, OcrJobError error, DateTimeOffset completedAt, CancellationToken cancellationToken = default)
    {
        if (!_byDocumentId.TryGetValue(documentId, out var record))
        {
            return Task.FromResult(false);
        }

        var updated = record with
        {
            Status = OcrStatus.Failed,
            CompletedAt = completedAt,
            Error = error
        };

        _byDocumentId[documentId] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateMonolithDocumentIdAsync(Guid documentId, int monolithDocumentId, CancellationToken cancellationToken = default)
    {
        if (!_byDocumentId.TryGetValue(documentId, out var record))
        {
            return Task.FromResult(false);
        }

        var updated = record with
        {
            MonolithDocumentId = monolithDocumentId
        };

        _byDocumentId[documentId] = updated;
        return Task.FromResult(true);
    }
}