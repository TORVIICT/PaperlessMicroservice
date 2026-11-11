using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OcrAcl.Application.Ports;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Isolation;

public sealed class IsolationTests
{
    private static readonly string[] SuccessFieldNames =
        { "confidence", "documentId", "language", "pageCount", "status", "text" };

    private static readonly string[] InternalMarkers =
        { "SIMULATED-OCR", "Simulated", "OcrAcl", "Infrastructure", "EngineDescriptor" };

    [Fact]
    [Trait("TestCaseId", "TC-ISO-001")]
    [Trait("Category", "ISO")]
    public async Task SuccessResponse_ContainsOnlyContractFields()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(SuccessFieldNames, names);                             // CT-09, M-04
    }

    [Fact]
    [Trait("TestCaseId", "TC-ISO-002")]
    [Trait("Category", "ISO")]
    public async Task SuccessResponse_DoesNotContainInternalMarkers()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));
        var body = await response.Content.ReadAsStringAsync();

        foreach (var marker in InternalMarkers)
        {
            Assert.DoesNotContain(marker, body);                            // CT-18, D-23
        }
    }

    [Fact]
    [Trait("TestCaseId", "TC-ISO-003")]
    [Trait("Category", "ISO")]
    public async Task ErrorResponse_DoesNotContainInternalNames()
    {
        using var invalidFactory = new OcrAclFactory();
        using var invalidClient = invalidFactory.CreateClient();
        using var invalidResponse = await invalidClient.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithMimeType("application/zip")));
        var invalidBody = await invalidResponse.Content.ReadAsStringAsync();

        using var faultyFactory = new OcrAclFactory(
            services =>
            {
                services.RemoveAll<IOcrEngine>();
                services.AddSingleton<IOcrEngine, FaultyOcrEngine>();
            });
        using var faultyClient = faultyFactory.CreateClient();
        using var faultyResponse = await faultyClient.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));
        var faultyBody = await faultyResponse.Content.ReadAsStringAsync();

        foreach (var body in new[] { invalidBody, faultyBody })
        {
            Assert.DoesNotContain("OcrAcl", body);                          // CT-18
            Assert.DoesNotContain("Infrastructure", body);                  // CT-18
            Assert.DoesNotContain("InvalidOperationException", body);       // CT-18
            Assert.DoesNotContain("StackTrace", body);                      // CT-18
            Assert.DoesNotContain("SIMULATED-OCR", body);                   // CT-18
            Assert.DoesNotContain("System.", body);                         // CT-18
        }
    }

    [Fact]
    [Trait("TestCaseId", "TC-ISO-004")]
    [Trait("Category", "ISO")]
    public async Task SuccessResponse_HeadersDoNotContainInternalMarkers()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        var headers = response.Headers
            .SelectMany(h => new[] { h.Key }.Concat(h.Value))
            .Concat(response.Content.Headers.SelectMany(h => new[] { h.Key }.Concat(h.Value)))
            .ToArray();

        foreach (var marker in InternalMarkers)
        {
            Assert.DoesNotContain(headers, value => value.Contains(marker, StringComparison.Ordinal)); // CT-18
        }
    }

    [Fact]
    [Trait("TestCaseId", "TC-ISO-005")]
    [Trait("Category", "ISO")]
    public async Task RecognizedText_DoesNotContainSimulationMarker()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = json.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.DoesNotContain("SIMULATED-OCR", text);                       // T-18, D-23
    }
}
