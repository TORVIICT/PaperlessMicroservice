using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Entities;
using Ocr.Domain.Enums;

namespace Ocr.Infrastructure.Persistence;

public class OcrDbContext : DbContext
{
    public OcrDbContext(DbContextOptions<OcrDbContext> options) : base(options)
    {
    }

    public DbSet<OcrJob> Jobs => Set<OcrJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OcrJob>(entity =>
        {
            entity.ToTable("ocr_jobs");
            entity.HasKey(j => j.JobId);
            entity.Property(j => j.JobId).HasColumnName("job_id");
            entity.Property(j => j.ClientReference).HasColumnName("client_reference").HasMaxLength(128).IsRequired();
            entity.Property(j => j.FileName).HasColumnName("file_name").HasMaxLength(256).IsRequired();
            entity.Property(j => j.ContentType).HasColumnName("content_type").HasMaxLength(128).IsRequired();
            entity.Property(j => j.SizeBytes).HasColumnName("size_bytes").IsRequired();
            entity.Property(j => j.FileStoragePath).HasColumnName("file_storage_path").HasMaxLength(1024);
            entity.Property(j => j.Language).HasColumnName("language").HasMaxLength(16).IsRequired();
            entity.Property(j => j.Mode).HasColumnName("mode").HasMaxLength(32).IsRequired();
            entity.Property(j => j.Status)
                  .HasColumnName("status")
                  .HasConversion<string>()
                  .HasMaxLength(32)
                  .IsRequired();
            entity.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(j => j.ProcessingStartedAt).HasColumnName("processing_started_at");
            entity.Property(j => j.CompletedAt).HasColumnName("completed_at");

            entity.OwnsOne(j => j.Result, result =>
            {
                result.Property(r => r.Text).HasColumnName("result_text");
                result.Property(r => r.PageCount).HasColumnName("result_page_count");
                result.Property(r => r.DurationMs).HasColumnName("result_duration_ms");
                result.Property(r => r.EngineVersion).HasColumnName("result_engine_version").HasMaxLength(128);
                result.Property(r => r.ArchivedPdfPath).HasColumnName("result_archived_pdf_path").HasMaxLength(1024);
                result.Property(r => r.ThumbnailPath).HasColumnName("result_thumbnail_path").HasMaxLength(1024);
            });

            entity.OwnsOne(j => j.Error, error =>
            {
                error.Property(e => e.Code).HasColumnName("error_code").HasMaxLength(64);
                error.Property(e => e.Message).HasColumnName("error_message");
            });
        });
    }
}
