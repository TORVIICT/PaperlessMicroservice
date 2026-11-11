using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;
using Ocr.Infrastructure.Persistence;

namespace Ocr.UnitTests;

public class EfOcrJobRepositoryTests
{
    private static OcrDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OcrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OcrDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_And_GetByIdAsync_PersistsAndRetrievesJob()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        var job = new OcrJob
        {
            ClientReference = "TEST-REF-001",
            FileName = "invoice.pdf",
            Language = "eng",
            Mode = "skip",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await repository.CreateAsync(job);
        Assert.NotNull(created);
        Assert.Equal(job.JobId, created.JobId);

        var retrieved = await repository.GetByIdAsync(job.JobId);
        Assert.NotNull(retrieved);
        Assert.Equal("TEST-REF-001", retrieved.ClientReference);
        Assert.Equal("invoice.pdf", retrieved.FileName);
        Assert.Equal(JobStatus.Pending, retrieved.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesJobStatusAndResult()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        var job = new OcrJob
        {
            ClientReference = "TEST-REF-002",
            FileName = "contract.pdf",
            Status = JobStatus.Pending
        };

        await repository.CreateAsync(job);

        // Update with Result
        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Result = new OcrJobResult(
            Text: "Sample extracted contract text",
            PageCount: 2,
            DurationMs: 1500,
            EngineVersion: "Test Engine 1.0");

        await repository.UpdateAsync(job);

        var updated = await repository.GetByIdAsync(job.JobId);
        Assert.NotNull(updated);
        Assert.Equal(JobStatus.Completed, updated.Status);
        Assert.NotNull(updated.Result);
        Assert.Equal("Sample extracted contract text", updated.Result.Text);
        Assert.Equal(2, updated.Result.PageCount);
        Assert.Equal(1500, updated.Result.DurationMs);
    }

    [Fact]
    public async Task TryClaimJobAsync_ClaimsPendingJobAndMarksItProcessing()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);
        var job = new OcrJob { ClientReference = "P1", FileName = "f1.pdf", Status = JobStatus.Pending };
        await repository.CreateAsync(job);

        var claimed = await repository.TryClaimJobAsync(job.JobId, DateTimeOffset.UtcNow);

        Assert.NotNull(claimed);
        Assert.Equal(JobStatus.Processing, claimed.Status);
        Assert.NotNull(claimed.ProcessingStartedAt);

        var stored = await repository.GetByIdAsync(job.JobId);
        Assert.Equal(JobStatus.Processing, stored!.Status);
    }

    [Fact]
    public async Task TryClaimJobAsync_DoesNotClaimCompletedJob()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);
        var job = new OcrJob { ClientReference = "C1", FileName = "f1.pdf", Status = JobStatus.Completed };
        await repository.CreateAsync(job);

        var claimed = await repository.TryClaimJobAsync(job.JobId, DateTimeOffset.UtcNow);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task TryClaimJobAsync_DoesNotReclaimFreshProcessingJob()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);
        var job = new OcrJob
        {
            ClientReference = "P2",
            FileName = "f2.pdf",
            Status = JobStatus.Processing,
            ProcessingStartedAt = DateTimeOffset.UtcNow
        };
        await repository.CreateAsync(job);

        var claimed = await repository.TryClaimJobAsync(
            job.JobId,
            staleBefore: DateTimeOffset.UtcNow.AddMinutes(-30));

        Assert.Null(claimed);
    }

    [Fact]
    public async Task TryClaimJobAsync_ReclaimsStaleProcessingJob()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);
        var job = new OcrJob
        {
            ClientReference = "P3",
            FileName = "f3.pdf",
            Status = JobStatus.Processing,
            ProcessingStartedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        await repository.CreateAsync(job);

        var claimed = await repository.TryClaimJobAsync(
            job.JobId,
            staleBefore: DateTimeOffset.UtcNow.AddMinutes(-30));

        Assert.NotNull(claimed);
        Assert.Equal(JobStatus.Processing, claimed.Status);
        Assert.True(claimed.ProcessingStartedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task TryClaimNextJobAsync_ReturnsOldestClaimableJob()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        var older = new OcrJob
        {
            ClientReference = "P1",
            FileName = "older.pdf",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var newer = new OcrJob
        {
            ClientReference = "P2",
            FileName = "newer.pdf",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var completed = new OcrJob
        {
            ClientReference = "C1",
            FileName = "completed.pdf",
            Status = JobStatus.Completed
        };

        await repository.CreateAsync(newer);
        await repository.CreateAsync(older);
        await repository.CreateAsync(completed);

        var first = await repository.TryClaimNextJobAsync(DateTimeOffset.UtcNow.AddMinutes(-30));
        var second = await repository.TryClaimNextJobAsync(DateTimeOffset.UtcNow.AddMinutes(-30));
        var third = await repository.TryClaimNextJobAsync(DateTimeOffset.UtcNow.AddMinutes(-30));

        Assert.Equal(older.JobId, first!.JobId);
        Assert.Equal(newer.JobId, second!.JobId);
        Assert.Null(third);
    }

    [Fact]
    public async Task ResetStaleProcessingJobsAsync_ReturnsStaleJobsToPending()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        var stale = new OcrJob
        {
            ClientReference = "S1",
            FileName = "stale.pdf",
            Status = JobStatus.Processing,
            ProcessingStartedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        var fresh = new OcrJob
        {
            ClientReference = "F1",
            FileName = "fresh.pdf",
            Status = JobStatus.Processing,
            ProcessingStartedAt = DateTimeOffset.UtcNow
        };
        var pending = new OcrJob
        {
            ClientReference = "P1",
            FileName = "pending.pdf",
            Status = JobStatus.Pending
        };

        await repository.CreateAsync(stale);
        await repository.CreateAsync(fresh);
        await repository.CreateAsync(pending);

        var recovered = await repository.ResetStaleProcessingJobsAsync(
            staleBefore: DateTimeOffset.UtcNow.AddMinutes(-30));

        Assert.Equal(1, recovered);

        var recoveredJob = await repository.GetByIdAsync(stale.JobId);
        Assert.Equal(JobStatus.Pending, recoveredJob!.Status);
        Assert.Null(recoveredJob.ProcessingStartedAt);

        var freshJob = await repository.GetByIdAsync(fresh.JobId);
        Assert.Equal(JobStatus.Processing, freshJob!.Status);
    }

    [Fact]
    public async Task GetAllAsync_WithStatus_FiltersItemsAndTotal()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        await repository.CreateAsync(new OcrJob
        {
            ClientReference = "Q1",
            FileName = "queued-1.pdf",
            Status = JobStatus.Pending
        });
        await repository.CreateAsync(new OcrJob
        {
            ClientReference = "Q2",
            FileName = "queued-2.pdf",
            Status = JobStatus.Pending
        });
        await repository.CreateAsync(new OcrJob
        {
            ClientReference = "C1",
            FileName = "completed.pdf",
            Status = JobStatus.Completed
        });

        var (items, total) = await repository.GetAllAsync(status: JobStatus.Pending);

        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
        Assert.All(items, job => Assert.Equal(JobStatus.Pending, job.Status));
    }

    [Fact]
    public async Task GetAllAsync_WithoutStatus_ReturnsAllItems()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new EfOcrJobRepository(dbContext);

        await repository.CreateAsync(new OcrJob
        {
            ClientReference = "Q1",
            FileName = "queued.pdf",
            Status = JobStatus.Pending
        });
        await repository.CreateAsync(new OcrJob
        {
            ClientReference = "F1",
            FileName = "failed.pdf",
            Status = JobStatus.Failed
        });

        var (items, total) = await repository.GetAllAsync();

        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
    }
}
