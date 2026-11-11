using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ocr.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations` can build the Npgsql model
/// without starting the API host or requiring a live database.
/// </summary>
public sealed class OcrDbContextFactory : IDesignTimeDbContextFactory<OcrDbContext>
{
    public OcrDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("OCR_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=ocr;Username=ocr;Password=ocr";

        var options = new DbContextOptionsBuilder<OcrDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OcrDbContext(options);
    }
}
