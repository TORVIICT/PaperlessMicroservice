var builder = WebApplication.CreateBuilder(args);

// Application layer services
builder.Services.AddSingleton<OcrAcl.Application.Ports.IClock, OcrAcl.Application.Ports.TimeProviderClock>(sp =>
    new OcrAcl.Application.Ports.TimeProviderClock(TimeProvider.System));
builder.Services.AddSingleton<OcrAcl.Application.Ports.IIdGenerator, OcrAcl.Application.Ports.DefaultIdGenerator>();
builder.Services.AddSingleton<OcrAcl.Application.Ports.ICorrelationIdAccessor, OcrAcl.Api.Services.HttpContextCorrelationIdAccessor>();
builder.Services.AddHttpContextAccessor();

// Polling policy (Infrastructure)
builder.Services.AddSingleton<OcrAcl.Infrastructure.Ocr.PollingPolicy>();

// OCR polling queue (Infrastructure) - D-34: in-memory
builder.Services.AddSingleton<OcrAcl.Application.Ports.IOcrPollingQueue, OcrAcl.Infrastructure.Store.InMemoryOcrPollingQueue>();

// OCR outbound headers handler (Infrastructure)
var ocrServiceToken = builder.Configuration["OCR_SERVICE_TOKEN"] ?? "test-token";
builder.Services.AddTransient(sp => new OcrAcl.Infrastructure.Ocr.OcrOutboundHeadersHandler(
    sp.GetRequiredService<OcrAcl.Application.Ports.ICorrelationIdAccessor>(),
    ocrServiceToken));

// Typed HttpClient for OCR service
var ocrServiceUrl = builder.Configuration["OCR_SERVICE_URL"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<OcrAcl.Application.Ports.IOcrClient, OcrAcl.Infrastructure.Ocr.HttpOcrClient>(client =>
{
    client.BaseAddress = new Uri(ocrServiceUrl);
    client.Timeout = Timeout.InfiniteTimeSpan; // We handle timeouts via CancellationToken and PollingPolicy
})
.AddHttpMessageHandler<OcrAcl.Infrastructure.Ocr.OcrOutboundHeadersHandler>();

// ACL store - in-memory implementation (D-34)
builder.Services.AddSingleton<OcrAcl.Application.Ports.IDocumentStore, OcrAcl.Infrastructure.Store.InMemoryDocumentStore>();

// Monolith client configuration
var monolithBaseUrl = builder.Configuration["MONOLITH_BASE_URL"] ?? "http://localhost:8000";
var monolithToken = builder.Configuration["MONOLITH_API_TOKEN"] ?? "";
builder.Services.AddSingleton(new OcrAcl.Infrastructure.Monolith.MonolithOptions
{
    BaseUrl = monolithBaseUrl,
    ApiToken = monolithToken
});

// Monolith outbound headers handler (Infrastructure)
builder.Services.AddTransient(sp => new OcrAcl.Infrastructure.Monolith.MonolithOutboundHeadersHandler(
    sp.GetRequiredService<OcrAcl.Application.Ports.ICorrelationIdAccessor>(),
    monolithToken));

// Monolith client - HTTP REST implementation (D-44: T-C public REST API)
builder.Services.AddHttpClient<OcrAcl.Application.Ports.IMonolithClient, OcrAcl.Infrastructure.Monolith.HttpMonolithClient>(client =>
{
    client.BaseAddress = new Uri(monolithBaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddHttpMessageHandler<OcrAcl.Infrastructure.Monolith.MonolithOutboundHeadersHandler>();

// Application use cases
builder.Services.AddScoped<OcrAcl.Application.Services.SubmitDocumentHandler>();
builder.Services.AddScoped<OcrAcl.Application.Services.GetArchiveHandler>();
builder.Services.AddScoped<OcrAcl.Application.Services.SynchronizeCompletedDocumentHandler>();

// Api layer translators
builder.Services.AddScoped<OcrAcl.Api.Translation.DocumentRequestTranslator>(sp =>
    new OcrAcl.Api.Translation.DocumentRequestTranslator(
        sp.GetRequiredService<OcrAcl.Application.Ports.IIdGenerator>(),
        defaultLanguage: "spa",   // D-37
        defaultMode: "skip",      // D-37
        maxFileSizeBytes: 50 * 1024 * 1024)); // D-35: 50 MB
builder.Services.AddScoped<OcrAcl.Api.Translation.DocumentResponseTranslator>();
builder.Services.AddScoped<OcrAcl.Application.Ports.IMonolithTranslator, OcrAcl.Api.Translation.MonolithTranslator>();

// The old delivery handler remains available for legacy/accompanied routes,
// but the active OCR completion path is SynchronizeCompletedDocumentHandler.
builder.Services.AddScoped<OcrAcl.Application.Services.DeliverResultToMonolithHandler>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Run();

public partial class Program { }
