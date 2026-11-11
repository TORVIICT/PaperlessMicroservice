using System.Net;
using System.Text.Json;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Validation;

public sealed class ValidationTests
{
    private static async Task<JsonDocument> PostAsync(string body)
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(body));
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    private static string[] DetailFields(JsonDocument json) =>
        json.RootElement.GetProperty("error").GetProperty("details")
            .EnumerateArray()
            .Select(d => d.GetProperty("field").GetString() ?? string.Empty)
            .ToArray();

    private static string Code(JsonDocument json) =>
        json.RootElement.GetProperty("error").GetProperty("code").GetString() ?? string.Empty;

    [Fact]
    [Trait("TestCaseId", "TC-VAL-001")]
    [Trait("Category", "VAL")]
    public async Task MissingDocumentId_Returns400WithFieldDetail()
    {
        using var json = await PostAsync(ContractSamples.WithoutProperty("documentId"));
        Assert.Equal("invalid_request", Code(json));                        // CT-03, CT-13
        Assert.Contains("documentId", DetailFields(json));                  // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-002")]
    [Trait("Category", "VAL")]
    public async Task MissingMimeType_Returns400WithFieldDetail()
    {
        using var json = await PostAsync(ContractSamples.WithoutProperty("mimeType"));
        Assert.Equal("invalid_request", Code(json));                        // CT-05, CT-13
        Assert.Contains("mimeType", DetailFields(json));                    // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-003")]
    [Trait("Category", "VAL")]
    public async Task MissingContentBase64_Returns400WithFieldDetail()
    {
        using var json = await PostAsync(ContractSamples.WithoutProperty("contentBase64"));
        Assert.Equal("invalid_request", Code(json));                        // CT-06, CT-13
        Assert.Contains("contentBase64", DetailFields(json));               // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-004")]
    [Trait("Category", "VAL")]
    public async Task MissingFileName_Returns400WithFieldDetail()
    {
        using var json = await PostAsync(ContractSamples.WithoutProperty("fileName"));
        Assert.Equal("invalid_request", Code(json));                        // CT-04, CT-13
        Assert.Contains("fileName", DetailFields(json));                    // CT-13
    }

    [Theory]
    [InlineData("documentId")]
    [Trait("TestCaseId", "TC-VAL-005")]
    [Trait("Category", "VAL")]
    public async Task NullDocumentId_Returns400(string property)
    {
        using var json = await PostAsync(ContractSamples.ValidRequest.Replace("\"doc-001\"", "null", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-03, CT-13
        Assert.Contains(property, DetailFields(json));                      // CT-13
    }

    [Theory]
    [InlineData("fileName")]
    [Trait("TestCaseId", "TC-VAL-006")]
    [Trait("Category", "VAL")]
    public async Task NullFileName_Returns400(string property)
    {
        using var json = await PostAsync(ContractSamples.ValidRequest.Replace("\"invoice.pdf\"", "null", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-04, CT-13
        Assert.Contains(property, DetailFields(json));                      // CT-13
    }

    [Theory]
    [InlineData("mimeType")]
    [Trait("TestCaseId", "TC-VAL-007")]
    [Trait("Category", "VAL")]
    public async Task NullMimeType_Returns400(string property)
    {
        using var json = await PostAsync(ContractSamples.ValidRequest.Replace("\"application/pdf\"", "null", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-05, CT-13
        Assert.Contains(property, DetailFields(json));                      // CT-13
    }

    [Theory]
    [InlineData("contentBase64")]
    [Trait("TestCaseId", "TC-VAL-008")]
    [Trait("Category", "VAL")]
    public async Task NullContentBase64_Returns400(string property)
    {
        using var json = await PostAsync(ContractSamples.ValidRequest.Replace("\"SGVsbG8gd29ybGQ=\"", "null", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-06, CT-13
        Assert.Contains(property, DetailFields(json));                      // CT-13
    }

    [Theory]
    [InlineData("language")]
    [Trait("TestCaseId", "TC-VAL-009")]
    [Trait("Category", "VAL")]
    public async Task NullLanguage_Returns400(string property)
    {
        using var json = await PostAsync(ContractSamples.WithLanguage("eng").Replace("\"eng\"", "null", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-07, CT-13
        Assert.Contains(property, DetailFields(json));                      // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-010")]
    [Trait("Category", "VAL")]
    public async Task WrongTypeDocumentId_Returns400()
    {
        var body = ContractSamples.ValidRequest.Replace("\"doc-001\"", "1", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-17
        Assert.Contains("documentId", DetailFields(json));                  // CT-03, D-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-011")]
    [Trait("Category", "VAL")]
    public async Task WrongTypeFileName_Returns400()
    {
        var body = ContractSamples.ValidRequest.Replace("\"invoice.pdf\"", "1", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-17
        Assert.Contains("fileName", DetailFields(json));                    // CT-04, D-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-012")]
    [Trait("Category", "VAL")]
    public async Task WrongTypeMimeType_Returns400()
    {
        var body = ContractSamples.ValidRequest.Replace("\"application/pdf\"", "1", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-17
        Assert.Contains("mimeType", DetailFields(json));                    // CT-05, D-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-013")]
    [Trait("Category", "VAL")]
    public async Task WrongTypeContentBase64_Returns400()
    {
        var body = ContractSamples.ValidRequest.Replace("\"SGVsbG8gd29ybGQ=\"", "true", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-17
        Assert.Contains("contentBase64", DetailFields(json));               // CT-06, D-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-014")]
    [Trait("Category", "VAL")]
    public async Task WrongTypeLanguage_Returns400()
    {
        var body = ContractSamples.ValidRequest.Replace("\"language\": \"eng\"", "\"language\": [\"eng\"]", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-17
        Assert.Contains("language", DetailFields(json));                    // CT-07, D-17
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-015")]
    [Trait("Category", "VAL")]
    public async Task EmptyDocumentId_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithDocumentId(string.Empty));
        Assert.Equal("invalid_request", Code(json));                        // CT-03, CT-13
        Assert.Contains("documentId", DetailFields(json));                  // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-016")]
    [Trait("Category", "VAL")]
    public async Task WhitespaceDocumentId_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithDocumentId("   "));
        Assert.Equal("invalid_request", Code(json));                        // CT-03, A-08, CT-13
        Assert.Contains("documentId", DetailFields(json));                  // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-017")]
    [Trait("Category", "VAL")]
    public async Task DocumentIdTooLong_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithDocumentId(new string('a', 65)));
        Assert.Equal("invalid_request", Code(json));                        // CT-03, CT-13
        Assert.Contains("documentId", DetailFields(json));                  // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-018")]
    [Trait("Category", "VAL")]
    public async Task EmptyFileName_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithFileName(string.Empty));
        Assert.Equal("invalid_request", Code(json));                        // CT-04, CT-13
        Assert.Contains("fileName", DetailFields(json));                    // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-019")]
    [Trait("Category", "VAL")]
    public async Task FileNameTooLong_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithFileName(new string('a', 256)));
        Assert.Equal("invalid_request", Code(json));                        // CT-04, CT-13
        Assert.Contains("fileName", DetailFields(json));                    // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-020")]
    [Trait("Category", "VAL")]
    public async Task UnsupportedMimeType_Returns400WithFieldDetail()
    {
        using var json = await PostAsync(ContractSamples.WithMimeType("application/zip"));
        Assert.Equal("invalid_request", Code(json));                        // CT-05, CT-13
        Assert.Contains("mimeType", DetailFields(json));                    // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-021")]
    [Trait("Category", "VAL")]
    public async Task UnsupportedLanguage_Returns400()
    {
        using var json = await PostAsync(ContractSamples.WithLanguage("fra"));
        Assert.Equal("invalid_request", Code(json));                        // CT-07, CT-13
        Assert.Contains("language", DetailFields(json));                    // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-022")]
    [Trait("Category", "VAL")]
    public async Task InvalidBase64_Returns400WithFieldDetail()
    {
        var body = ContractSamples.ValidRequest.Replace("\"SGVsbG8gd29ybGQ=\"", "\"not-base64!!\"", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-06, CT-13
        Assert.Contains("contentBase64", DetailFields(json));               // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-023")]
    [Trait("Category", "VAL")]
    public async Task EmptyContentBase64_Returns400()
    {
        using var json = await PostAsync(ContractSamples.ValidRequest.Replace("\"SGVsbG8gd29ybGQ=\"", "\"\"", StringComparison.Ordinal));
        Assert.Equal("invalid_request", Code(json));                        // CT-06, CT-13
        Assert.Contains("contentBase64", DetailFields(json));               // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-024")]
    [Trait("Category", "VAL")]
    public async Task MalformedJson_Returns400WithEmptyDetails()
    {
        using var json = await PostAsync("{");
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-06
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength()); // A-04
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-025")]
    [Trait("Category", "VAL")]
    public async Task EmptyBody_Returns400WithEmptyDetails()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);       // CT-13, D-07
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", Code(json));                        // CT-13, D-07
        Assert.Equal(0, json.RootElement.GetProperty("error").GetProperty("details").GetArrayLength()); // A-04
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-026")]
    [Trait("Category", "VAL")]
    public async Task MultipleInvalidFields_ReturnsOneDetailPerField()
    {
        var body = ContractSamples.WithMimeType("application/zip")
            .Replace("\"language\": \"eng\"", "\"language\": \"fra\"", StringComparison.Ordinal);
        using var json = await PostAsync(body);
        Assert.Equal("invalid_request", Code(json));                        // CT-13
        var fields = DetailFields(json);
        Assert.Equal(2, fields.Length);                                     // CT-13
        Assert.Contains("mimeType", fields);                                // CT-13
        Assert.Contains("language", fields);                                // CT-13
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-027")]
    [Trait("Category", "VAL")]
    public async Task UnknownJsonProperty_IsIgnored()
    {
        var body = ContractSamples.ValidRequest.Replace(
            "\"language\": \"eng\"",
            "\"language\": \"eng\", \"priority\": 1",
            StringComparison.Ordinal);
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // D-16
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("doc-001", json.RootElement.GetProperty("documentId").GetString()); // D-16
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-028")]
    [Trait("Category", "VAL")]
    public async Task DocumentIdBoundary64_IsAccepted()
    {
        var documentId = new string('a', 64);
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithDocumentId(documentId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-03
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(documentId, json.RootElement.GetProperty("documentId").GetString()); // CT-09
    }

    [Fact]
    [Trait("TestCaseId", "TC-VAL-029")]
    [Trait("Category", "VAL")]
    public async Task FileNameBoundary255_IsAccepted()
    {
        var fileName = new string('a', 255);
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(ContractSamples.WithFileName(fileName)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);               // CT-04
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("doc-001", json.RootElement.GetProperty("documentId").GetString()); // CT-08
    }
}
