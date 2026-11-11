using System.Net;
using System.Text.Json;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Integration;

public sealed class PipelineTests
{
    [Fact]
    [Trait("TestCaseId", "TC-INT-001")]
    [Trait("Category", "INT")]
    public async Task Documents_ValidRequest_Returns200Json()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-08
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType); // CT-08
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-002")]
    [Trait("Category", "INT")]
    public async Task Health_Get_Returns200WithStatusOk()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ContractSamples.HealthRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-11
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString()); // CT-11
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-003")]
    [Trait("Category", "INT")]
    public async Task UnknownRoute_Returns404ContractError()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/ocr/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);         // CT-15
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-15
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength());   // CT-15
    }

    [Theory]
    [InlineData("POST", "/api/ocr/health")]
    [InlineData("GET", "/api/ocr/documents")]
    [Trait("TestCaseId", "TC-INT-004")]
    [Trait("Category", "INT")]
    public async Task KnownRoute_WrongMethod_Returns405ContractError(string method, string route)
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode); // CT-16
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("method_not_allowed", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-16
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength());           // CT-16
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [Trait("TestCaseId", "TC-INT-005")]
    [Trait("Category", "INT")]
    public async Task WrongContentType_Returns415ContractError(string mediaType)
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.ContentWithMediaType(ContractSamples.ValidRequest, mediaType));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode); // CT-14
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unsupported_media_type", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-14
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength());               // CT-14
    }

    [Fact]
    [Trait("TestCaseId", "TC-INT-006")]
    [Trait("Category", "INT")]
    public async Task MissingContentType_Returns415ContractError()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContentWithoutContentType(ContractSamples.ValidRequest));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode); // CT-14, A-06
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unsupported_media_type", json.RootElement.GetProperty("error").GetProperty("code").GetString()); // CT-14, A-06
    }
}
