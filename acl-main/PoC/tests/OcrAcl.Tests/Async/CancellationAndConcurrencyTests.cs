using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OcrAcl.Application.Ports;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Async;

public sealed class CancellationAndConcurrencyTests
{
    [Fact]
    [Trait("TestCaseId", "TC-ASY-001")]
    [Trait("Category", "ASY")]
    public async Task PreCancelledToken_ThrowsOperationCanceled()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var response = await client.PostAsync(
                ContractSamples.DocumentsRoute,
                HttpTestHelpers.JsonContent(ContractSamples.ValidRequest),
                cts.Token);
        });
    }

    [Fact]
    [Trait("TestCaseId", "TC-ASY-002")]
    [Trait("Category", "ASY")]
    public async Task CancelledRequest_ObservesCancellationInEngine()
    {
        var engine = new BlockingOcrEngine();
        using var factory = new OcrAclFactory(services =>
        {
            services.RemoveAll<IOcrEngine>();
            services.AddSingleton<IOcrEngine>(engine);
        });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var request = client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest),
            cts.Token);

        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request); // D-27
        await engine.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(30)); // TS-23
    }

    [Fact]
    [Trait("TestCaseId", "TC-ASY-003")]
    [Trait("Category", "ASY")]
    public async Task ConcurrentDifferentRequests_EachGetTheirOwnResult()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        var ids = new[] { "doc-001", "doc-002", "doc-003" };
        var tasks = ids.Select(id => client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithDocumentId(id)))).ToArray();

        var responses = await Task.WhenAll(tasks);

        for (var i = 0; i < ids.Length; i++)
        {
            using var json = JsonDocument.Parse(await responses[i].Content.ReadAsStringAsync());
            Assert.Equal(ids[i], json.RootElement.GetProperty("documentId").GetString()); // D-28
            responses[i].Dispose();
        }
    }
}
