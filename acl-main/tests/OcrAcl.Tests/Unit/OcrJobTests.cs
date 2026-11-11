using OcrAcl.Application.Models;

namespace OcrAcl.Tests.Unit;

/// <summary>
/// Unit tests for the <see cref="OcrJob"/> model.
/// </summary>
public sealed class OcrJobTests
{
    [Fact]
    [Trait("TestCaseId", "TC-UNIT-001")]
    [Trait("Category", "UNIT")]
    public void OcrJob_CanBeCreated_WithAllProperties()
    {
        var jobId = Guid.NewGuid();
        var clientRef = "doc-ref-abc123";
        var createdAt = DateTimeOffset.UtcNow;
        var completedAt = createdAt.AddMinutes(5);
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 2,
            DurationMs = 150,
            EngineVersion = "ENGINE",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "url1",
            HasThumbnail = true,
            ThumbnailUrl = "url2"
        };
        var error = new OcrJobError { Code = "ENGINE_ERROR", Message = "Failed" };

        var job = new OcrJob
        {
            JobId = jobId,
            ClientReference = clientRef,
            Status = OcrStatus.Completed,
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            Result = result,
            Error = null
        };

        Assert.Equal(jobId, job.JobId);
        Assert.Equal(clientRef, job.ClientReference);
        Assert.Equal(OcrStatus.Completed, job.Status);
        Assert.Equal(createdAt, job.CreatedAt);
        Assert.Equal(completedAt, job.CompletedAt);
        Assert.Equal(result, job.Result);
        Assert.Null(job.Error);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-002")]
    [Trait("Category", "UNIT")]
    public void OcrJob_WithFailedStatus_HasErrorAndNullResult()
    {
        var job = new OcrJob
        {
            JobId = Guid.NewGuid(),
            ClientReference = "doc-ref-xyz789",
            Status = OcrStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Result = null,
            Error = new OcrJobError { Code = "CORRUPT_DOCUMENT", Message = "Corrupt" }
        };

        Assert.Equal(OcrStatus.Failed, job.Status);
        Assert.NotNull(job.Error);
        Assert.Equal("CORRUPT_DOCUMENT", job.Error!.Code);
        Assert.Null(job.Result);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-003")]
    [Trait("Category", "UNIT")]
    public void OcrJob_WithProcessingStatus_HasNullCompletedAtAndNullResult()
    {
        var job = new OcrJob
        {
            JobId = Guid.NewGuid(),
            ClientReference = "doc-ref-processing",
            Status = OcrStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = null,
            Result = null,
            Error = null
        };

        Assert.Equal(OcrStatus.Processing, job.Status);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.Result);
        Assert.Null(job.Error);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-004")]
    [Trait("Category", "UNIT")]
    public void OcrJob_WithPendingStatus_IsNonTerminal()
    {
        var job = new OcrJob
        {
            JobId = Guid.NewGuid(),
            ClientReference = "doc-ref-pending",
            Status = OcrStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = null
        };

        Assert.Equal(OcrStatus.Pending, job.Status);
        Assert.True(job.Status.IsNonTerminal());
        Assert.False(job.Status.IsTerminal());
    }
}