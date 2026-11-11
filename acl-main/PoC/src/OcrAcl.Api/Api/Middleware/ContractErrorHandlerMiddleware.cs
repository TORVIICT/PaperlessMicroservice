using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using OcrAcl.Api.Contracts;

namespace OcrAcl.Api.Middleware;

public sealed class ContractErrorHandlerMiddleware
{
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ContractErrorHandlerMiddleware> _logger;

    public ContractErrorHandlerMiddleware(RequestDelegate next, ILogger<ContractErrorHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var statusCodeFeature = context.Features.Get<IStatusCodePagesFeature>();
        if (statusCodeFeature is not null)
        {
            statusCodeFeature.Enabled = false;
        }

        try
        {
            await _next(context);

            if (!context.Response.HasStarted && context.Response.ContentLength is null &&
                context.Response.StatusCode is >= 400)
            {
                await WriteErrorAsync(context, context.Response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode)
    {
        var (resolvedStatus, body) = ContractErrorCodes.ForStatusCode(statusCode);

        context.Response.Clear();
        context.Response.StatusCode = resolvedStatus;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, WriteOptions));
    }
}
