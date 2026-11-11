using Microsoft.EntityFrameworkCore;
using Ocr.Application.Ports;
using Ocr.Application.Services;
using Ocr.Infrastructure.Engines;
using Ocr.Infrastructure.Persistence;
using Ocr.Infrastructure.Notifications;
using Ocr.Infrastructure.Queues;
using Ocr.Infrastructure.Workers;
using Ocr.Worker;

var builder = Host.CreateApplicationBuilder(args);
var workerSettings = WorkerSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(workerSettings);

// 1. Configure Database
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
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
builder.Services.AddSingleton<IOcrCompletionNotifier, HttpOcrCompletionNotifier>();

// The worker never enqueues jobs, but OcrJobService depends on the port.
builder.Services.AddSingleton<IOcrJobQueue, ExternalWorkerJobQueue>();

// 2. Configure OCR Engine
builder.Services.AddSingleton<BasicOcrProcessingEngine>();
builder.Services.AddSingleton<IOcrProcessingEngine>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OcrMyPdfProcessingEngine>>();
    var fallback = sp.GetRequiredService<BasicOcrProcessingEngine>();
    var ocrmypdfPath = builder.Configuration["Ocr:EnginePath"] ?? Environment.GetEnvironmentVariable("OCRMYPDF_PATH");
    return new OcrMyPdfProcessingEngine(logger, fallback, ocrmypdfPath);
});

// 3. Application Services & Worker
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
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<WorkerHeartbeat>();

var host = builder.Build();

// Apply versioned schema migrations. InMemory (tests/local fallback) skips this.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OcrDbContext>();
    await DatabaseInitializer.MigrateAsync(db, host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"));
}

host.Run();
