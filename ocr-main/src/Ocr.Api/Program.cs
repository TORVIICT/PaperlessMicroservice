using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Ocr.Application.Ports;
using Ocr.Application.Services;
using Ocr.Infrastructure.Engines;
using Ocr.Infrastructure.Persistence;
using Ocr.Infrastructure.Notifications;
using Ocr.Infrastructure.Queues;
using Ocr.Infrastructure.Workers;

// The controller enforces the 50 MB business limit. The server limit is
// slightly higher so oversized uploads still receive the structured
// FILE_TOO_LARGE error instead of a bare 413 from the transport.
const long MaxRequestSizeBytes = 55L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxRequestSizeBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxRequestSizeBytes;
});

// Format model validation errors matching the frontend contract
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(er => er.ErrorMessage).ToArray()
            );

        var response = new Ocr.Api.Models.ApiErrorResponse(
            Code: "INVALID_FILE",
            Message: "El archivo o los parámetros enviados son inválidos.",
            DocumentId: null,
            Details: errors);

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
    };
});

// Configure CORS for Frontend direct consumption (Midterm) and exposed Location header
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Location");
    });
});

// 1. Configure EF Core Database (PostgreSQL with fallback to InMemory for tests/local dev)
var connectionString = builder.Configuration.GetConnectionString("Ocr")
    ?? builder.Configuration.GetConnectionString("OcrDb")
    ?? Environment.GetEnvironmentVariable("OCR_DB_CONNECTION_STRING");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<OcrDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<OcrDbContext>(options =>
        options.UseInMemoryDatabase("OcrDb"));
}

builder.Services.AddScoped<IOcrJobRepository, EfOcrJobRepository>();

var completionOptions = new OcrCompletionNotificationOptions
{
    Enabled = builder.Configuration.GetValue("AclNotification:Enabled", false),
    CompletionUrl = builder.Configuration["AclNotification:CompletionUrl"]
        ?? "http://localhost:5001/api/internal/v1/ocr/completed",
    InternalToken = builder.Configuration["AclNotification:InternalToken"] ?? string.Empty,
    MaxAttempts = builder.Configuration.GetValue("AclNotification:MaxAttempts", 3),
    InitialDelay = TimeSpan.FromSeconds(builder.Configuration.GetValue("AclNotification:InitialDelaySeconds", 1))
};
builder.Services.AddSingleton(completionOptions);
builder.Services.AddHttpClient<IOcrCompletionNotifier, HttpOcrCompletionNotifier>(client =>
    client.Timeout = TimeSpan.FromMinutes(5));

// 2. Configure job dispatching. Exactly one processing mode is active:
//    - embedded: this process runs the background worker (local development).
//    - external: jobs stay Pending for the separate Ocr.Worker container.
var workerMode = builder.Configuration["Worker:Mode"] ?? "embedded";
var workerSettings = WorkerSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(workerSettings);

switch (workerMode.ToLowerInvariant())
{
    case "embedded":
        builder.Services.AddSingleton<IOcrJobQueue, OcrJobChannelQueue>();
        builder.Services.AddHostedService<OcrBackgroundWorker>();
        break;

    case "external":
        builder.Services.AddSingleton<IOcrJobQueue, ExternalWorkerJobQueue>();
        break;

    default:
        throw new InvalidOperationException(
            $"Invalid Worker:Mode '{workerMode}'. Expected 'embedded' or 'external'.");
}

// 3. Configure OCR Engine (OCRmyPDF external process with fallback to Basic engine)
builder.Services.AddSingleton<BasicOcrProcessingEngine>();
builder.Services.AddSingleton<IOcrProcessingEngine>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OcrMyPdfProcessingEngine>>();
    var fallback = sp.GetRequiredService<BasicOcrProcessingEngine>();
    var ocrmypdfPath = builder.Configuration["Ocr:EnginePath"] ?? Environment.GetEnvironmentVariable("OCRMYPDF_PATH");
    return new OcrMyPdfProcessingEngine(logger, fallback, ocrmypdfPath);
});

// 4. Application Services
var storagePath = builder.Configuration["Storage:BasePath"] ?? Environment.GetEnvironmentVariable("STORAGE_BASE_PATH");
builder.Services.AddScoped<OcrJobService>(sp =>
{
    var repo = sp.GetRequiredService<IOcrJobRepository>();
    var engine = sp.GetRequiredService<IOcrProcessingEngine>();
    var logger = sp.GetRequiredService<ILogger<OcrJobService>>();
    var queue = sp.GetRequiredService<IOcrJobQueue>();
    var notifier = sp.GetRequiredService<IOcrCompletionNotifier>();
    return new OcrJobService(repo, engine, logger, queue, storagePath, workerSettings.StaleJobTimeout, notifier);
});

var app = builder.Build();

app.Logger.LogInformation("OCR worker mode: {WorkerMode}", workerMode);

// Versioned schema instead of EnsureCreated. InMemory (tests/local fallback)
// skips this step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OcrDbContext>();
    await DatabaseInitializer.MigrateAsync(db, app.Logger);
}

app.UseCors();

app.MapControllers();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", (OcrDbContext db) =>
    db.Database.CanConnect() ? Results.Ok(new { status = "ready" }) : Results.Problem("Database unavailable"));

app.Run();

public partial class Program { }
