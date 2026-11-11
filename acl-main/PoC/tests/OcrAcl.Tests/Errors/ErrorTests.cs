using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OcrAcl.Application.Ports;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Errors;

public sealed class ErrorTests
{
    private static OcrAclFactory FaultyFactory(string? environment = null) =>
        new(
            services =>
            {
                services.RemoveAll<IOcrEngine>();
                services.AddSingleton<IOcrEngine, FaultyOcrEngine>();
            },
            environment);

    [Fact]
    [Trait("TestCaseId", "TC-ERR-001")]
    [Trait("Category", "ERR")]
    public async Task UnexpectedFailure_Returns500InternalError()
    {
        using var factory = FaultyFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // CT-17
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("internal_error", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-17
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength());       // CT-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-ERR-002")]
    [Trait("Category", "ERR")]
    public async Task UnexpectedFailure_InProduction_Returns500InternalError()
    {
        using var factory = FaultyFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // CT-17, T-26
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("internal_error", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-17, T-26
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Production")]
    [Trait("TestCaseId", "TC-ERR-003")]
    [Trait("Category", "ERR")]
    public async Task UnexpectedFailure_DoesNotLeakInternalDetails(string? environment)
    {
        using var factory = FaultyFactory(environment);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(FaultyOcrEngine.Marker, body);                // CT-18
        Assert.DoesNotContain("InvalidOperationException", body);           // CT-18
        Assert.DoesNotContain("StackTrace", body);                          // CT-18
        Assert.DoesNotContain("OcrAcl", body);                              // CT-18
        Assert.DoesNotContain("Infrastructure", body);                      // CT-18
    }

    [Fact]
    [Trait("TestCaseId", "TC-ERR-004")]
    [Trait("Category", "ERR")]
    public async Task UnexpectedFailure_BodyHasContractShape()
    {
        using var factory = FaultyFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var topLevel = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "error" }, topLevel);                          // CT-12
        var error = json.RootElement.GetProperty("error");
        Assert.Equal(JsonValueKind.String, error.GetProperty("code").ValueKind);    // CT-12, CT-17
        Assert.Equal(JsonValueKind.String, error.GetProperty("message").ValueKind); // CT-12, CT-17
        Assert.Equal(JsonValueKind.Array, error.GetProperty("details").ValueKind);  // CT-12, CT-17
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Production")]
    [Trait("TestCaseId", "TC-ERR-005")]
    [Trait("Category", "ERR")]
    public async Task InternalErrorMessage_IsFixedAndGeneric(string? environment)
    {
        using var factory = FaultyFactory(environment);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("An unexpected error occurred.",
            json.RootElement.GetProperty("error").GetProperty("message").GetString()); // CT-17, D-18
    }
}
