using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OcrAcl.Application.Models;
using OcrAcl.Application.Ports;
using OcrAcl.Application.Services;
using OcrAcl.Infrastructure.Monolith;
using OcrAcl.Infrastructure.Ocr;
using OcrAcl.Infrastructure.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace OcrAcl.Tests.Integration;

/// <summary>
/// End-to-end acceptance test for the monolith hop (T-116).
/// Tests the complete T-C flow against a real Paperless-ngx instance.
/// </summary>
public class MonolithHopEndToEndTests : IAsyncLifetime
{
    private readonly string _monolithBaseUrl;
    private readonly string _monolithToken;
    private IHost? _host;
    private IServiceProvider? _services;

    public MonolithHopEndToEndTests()
    {
        _monolithBaseUrl = Environment.GetEnvironmentVariable("MONOLITH_BASE_URL") ?? "http://localhost:8010";
        _monolithToken = Environment.GetEnvironmentVariable("MONOLITH_API_TOKEN") ?? "0558926a83002fd97187e6cda172c8e8a5dfddb3";
    }

    public async Task InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        
        builder.Services.AddSingleton<IClock>(new TimeProviderClock(TimeProvider.System));
        builder.Services.AddSingleton<IIdGenerator, DefaultIdGenerator>();
        builder.Services.AddSingleton<ICorrelationIdAccessor>(new FakeCorrelationIdAccessor());
        builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();
        
        builder.Services.AddSingleton<PollingPolicy>(sp => 
            new PollingPolicy(sp.GetRequiredService<IClock>()));
        
        var monolithOptions = new MonolithOptions
        {
            BaseUrl = _monolithBaseUrl,
            ApiToken = _monolithToken,
            GlobalTimeout = TimeSpan.FromMinutes(5),
            InitialWait = TimeSpan.FromMilliseconds(500),
            MaxPollInterval = TimeSpan.FromSeconds(5),
            MaxPollAttempts = 60
        };
        builder.Services.AddSingleton(monolithOptions);
        
        builder.Services.AddHttpClient("MonolithClient", client =>
        {
            client.BaseAddress = new Uri(_monolithBaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<MonolithOutboundHeadersHandler>();
        
        builder.Services.AddTransient<MonolithOutboundHeadersHandler>(sp => 
            new MonolithOutboundHeadersHandler(
                sp.GetRequiredService<ICorrelationIdAccessor>(),
                _monolithToken));
        
        builder.Services.AddHttpClient<HttpMonolithClient>("MonolithClient")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(_monolithBaseUrl);
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<MonolithOutboundHeadersHandler>();
        
        builder.Services.AddTransient<MonolithOutboundHeadersHandler>(sp => 
            new MonolithOutboundHeadersHandler(
                sp.GetRequiredService<ICorrelationIdAccessor>(),
                _monolithToken));
        
        builder.Services.AddSingleton<IMonolithClient>(sp => 
            new HttpMonolithClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("MonolithClient"),
                sp.GetRequiredService<MonolithOptions>(),
                sp.GetRequiredService<PollingPolicy>()));
        
        _host = builder.Build();
        _services = _host.Services;
        await _host.StartAsync();
    }

    [Fact]
    public async Task T116_MonolithHop_EndToEnd_UploadsDocumentAndPatchesContent()
    {
        var monolithClient = _services!.GetRequiredService<IMonolithClient>();
        
        using var testFile = new MemoryStream(Encoding.UTF8.GetBytes("Test document for ACL integration test."));
        var taskId = await monolithClient.UploadDocumentAsync(
            testFile,
            "e2e-test.txt",
            "text/plain",
            "E2E Test Document",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(taskId));

        var taskResult = await monolithClient.PollTaskAsync(taskId, CancellationToken.None);
        
        Assert.Equal("success", taskResult.Status);
        Assert.NotNull(taskResult.DocumentId);
        
        var documentId = taskResult.DocumentId!.Value;
        
        await monolithClient.PatchContentAsync(
            documentId,
            "Updated content from ACL integration test.",
            CancellationToken.None);

        using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8010") };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", "0558926a83002fd97187e6cda172c8e8a5dfddb3");
        
        var getResponse = await httpClient.GetAsync($"/api/documents/{documentId}/", CancellationToken.None);
        Assert.True(getResponse.IsSuccessStatusCode);
        
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Contains("Updated content from ACL integration test", content);
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}