using System.Net;
using System.Text.Json;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Contract;

public sealed class ContractTests
{
    private static readonly string[] SuccessFieldNames =
        { "confidence", "documentId", "language", "pageCount", "status", "text" };

    [Fact]
    [Trait("TestCaseId", "TC-CON-001")]
    [Trait("Category", "CON")]
    public async Task Post_ValidContractSample_ReturnsContractShape()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-10
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(SuccessFieldNames, names);                             // CT-09, CT-10
    }

    [Fact]
    [Trait("TestCaseId", "TC-CON-002")]
    [Trait("Category", "CON")]
    public async Task Post_ValidContractSample_ReturnsContractLiterals()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("doc-001", root.GetProperty("documentId").GetString());  // CT-09
        Assert.Equal("completed", root.GetProperty("status").GetString());    // CT-09
        Assert.Equal("eng", root.GetProperty("language").GetString());        // CT-09
        Assert.True(root.GetProperty("pageCount").GetInt32() >= 1);           // CT-09
        Assert.InRange(root.GetProperty("confidence").GetDouble(), 0.0, 1.0); // CT-09
        Assert.Equal(JsonValueKind.String, root.GetProperty("text").ValueKind); // CT-09
    }

    [Fact]
    [Trait("TestCaseId", "TC-CON-003")]
    [Trait("Category", "CON")]
    public async Task Post_ResponseJsonKindsMatchContract()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.ValidRequest));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("documentId").ValueKind); // CT-09
        Assert.Equal(JsonValueKind.String, root.GetProperty("status").ValueKind);     // CT-09
        Assert.Equal(JsonValueKind.String, root.GetProperty("text").ValueKind);       // CT-09
        Assert.Equal(JsonValueKind.Number, root.GetProperty("pageCount").ValueKind);  // CT-09
        Assert.Equal(JsonValueKind.String, root.GetProperty("language").ValueKind);   // CT-09
        Assert.Equal(JsonValueKind.Number, root.GetProperty("confidence").ValueKind); // CT-09
    }

    [Fact]
    [Trait("TestCaseId", "TC-CON-004")]
    [Trait("Category", "CON")]
    public async Task Health_ContractSample_ReturnsExactBody()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ContractSamples.HealthRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-11
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "status" }, names);                            // CT-11
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("status").ValueKind);
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString()); // CT-11
    }

    [Fact]
    [Trait("TestCaseId", "TC-CON-005")]
    [Trait("Category", "CON")]
    public async Task Post_SpaLanguageSample_EchoesSpa()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithLanguage("spa")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-09
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("spa", json.RootElement.GetProperty("language").GetString()); // CT-07, CT-09
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [Trait("TestCaseId", "TC-CON-006")]
    [Trait("Category", "CON")]
    public async Task Post_AcceptsEveryContractMimeType(string mimeType)
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithMimeType(mimeType)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-05
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("doc-001", json.RootElement.GetProperty("documentId").GetString()); // CT-08
    }

    [Fact]
    [Trait("TestCaseId", "TC-CON-007")]
    [Trait("Category", "CON")]
    public async Task Error_InvalidRequest_HasContractErrorShape()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithMimeType("application/zip")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);       // CT-12
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var topLevel = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "error" }, topLevel);                          // CT-12
        var error = json.RootElement.GetProperty("error");
        var errorFields = error.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "code", "details", "message" }, errorFields);  // CT-12
        var detail = error.GetProperty("details")[0];
        var detailFields = detail.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "field", "message" }, detailFields);           // CT-12
    }
}
