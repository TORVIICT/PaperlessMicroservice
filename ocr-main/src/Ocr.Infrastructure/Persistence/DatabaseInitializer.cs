using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ocr.Infrastructure.Persistence;

/// <summary>
/// Applies versioned EF Core migrations for relational databases. The InMemory
/// provider used by tests and local fallback needs no schema setup.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAsync(
        OcrDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        logger.LogInformation("Applying OCR database migrations.");
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("OCR database migrations applied.");
    }
}
