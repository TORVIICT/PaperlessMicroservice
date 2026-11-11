using OcrAcl.Api.Middleware;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;
using OcrAcl.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IOcrEngine, SimulatedOcrEngine>();
builder.Services.AddScoped<OcrDocumentService>();

var app = builder.Build();

app.UseMiddleware<ContractErrorHandlerMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
