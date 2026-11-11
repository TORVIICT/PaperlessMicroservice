using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ocr.Infrastructure.Persistence;
using Ocr.Infrastructure.Workers;

namespace Ocr.IntegrationTests;

/// <summary>
/// Each test gets its own in-memory database and the embedded worker is
/// disabled, so uploads stay "queued" and the assertions are deterministic.
/// The shared "OcrDb" used by the lifecycle tests cannot be used here because
/// xUnit runs test classes in parallel.
/// </summary>
public class DocumentsFilterIntegrationTests
{
    private sealed class FilterFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"documents-filter-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove only the background consumer: uploads must stay queued
                // so the list filter can be asserted deterministically.
                var workerRegistrations = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType == typeof(OcrBackgroundWorker))
                    .ToList();

                foreach (var registration in workerRegistrations)
                {
                    services.Remove(registration);
                }

                var dbRegistrations = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(OcrDbContext) ||
                        (descriptor.ServiceType.IsGenericType &&
                         descriptor.ServiceType.GetGenericArguments().Contains(typeof(OcrDbContext))))
                    .ToList();

                foreach (var registration in dbRegistrations)
                {
                    services.Remove(registration);
                }

                services.AddDbContext<OcrDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes("%PDF-1.4 filter fixture content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/v1/documents", form);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return Guid.Parse(doc.RootElement.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task ListDocuments_FilterByQueued_ReturnsOnlyQueued()
    {
        using var factory = new FilterFactory();
        using var client = factory.CreateClient();

        var firstId = await UploadAsync(client, "filter-queued-1.pdf");
        await UploadAsync(client, "filter-queued-2.pdf");

        var response = await client.GetAsync("/api/v1/documents?status=queued&page=1&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, items.GetArrayLength());
        Assert.All(
            items.EnumerateArray(),
            item => Assert.Equal("queued", item.GetProperty("status").GetString()));
        Assert.Contains(
            items.EnumerateArray(),
            item => item.GetProperty("id").GetString() == firstId.ToString());
    }

    [Fact]
    public async Task ListDocuments_FilterByCompleted_ReturnsEmpty_AndTotalZero()
    {
        using var factory = new FilterFactory();
        using var client = factory.CreateClient();

        await UploadAsync(client, "filter-completed.pdf");

        var response = await client.GetAsync("/api/v1/documents?status=completed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(0, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ListDocuments_WithoutStatus_IncludesEveryDocument()
    {
        using var factory = new FilterFactory();
        using var client = factory.CreateClient();

        await UploadAsync(client, "filter-all-1.pdf");
        await UploadAsync(client, "filter-all-2.pdf");

        var response = await client.GetAsync("/api/v1/documents?page=1&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }
}
