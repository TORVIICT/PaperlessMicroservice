using System.Text.Json;
using OcrAcl.Tests.Fixtures;

namespace OcrAcl.Tests.Determinism;

public sealed class DeterminismTests
{
    private static async Task<string> PostAsync(HttpClient client, string body)
    {
        using var response = await client.PostAsync(
            ContractSamples.DocumentsRoute,
            HttpTestHelpers.JsonContent(body));
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    [Trait("TestCaseId", "TC-DET-001")]
    [Trait("Category", "DET")]
    public async Task SameRequest_RepeatedOnOneInstance_IsByteIdentical()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        var first = await PostAsync(client, ContractSamples.ValidRequest);
        for (var i = 0; i < 4; i++)
        {
            var next = await PostAsync(client, ContractSamples.ValidRequest);
            Assert.Equal(first, next);                                      // D-20
        }
    }

    [Fact]
    [Trait("TestCaseId", "TC-DET-002")]
    [Trait("Category", "DET")]
    public async Task SameRequest_OnFreshFactories_IsIdentical()
    {
        string first;
        using (var factory = new OcrAclFactory())
        using (var client = factory.CreateClient())
        {
            first = await PostAsync(client, ContractSamples.ValidRequest);
        }

        string second;
        using (var factory = new OcrAclFactory())
        using (var client = factory.CreateClient())
        {
            second = await PostAsync(client, ContractSamples.ValidRequest);
        }

        Assert.Equal(first, second);                                        // D-20
    }

    [Fact]
    [Trait("TestCaseId", "TC-DET-003")]
    [Trait("Category", "DET")]
    public async Task CallOrder_DoesNotChangeResponses()
    {
        var bodyA = ContractSamples.WithDocumentId("doc-aaa");
        var bodyB = ContractSamples.WithDocumentId("doc-bbb");

        string aThenB_A, aThenB_B, bThenA_A, bThenA_B;
        using (var factory = new OcrAclFactory())
        using (var client = factory.CreateClient())
        {
            aThenB_A = await PostAsync(client, bodyA);
            aThenB_B = await PostAsync(client, bodyB);
        }

        using (var factory = new OcrAclFactory())
        using (var client = factory.CreateClient())
        {
            bThenA_B = await PostAsync(client, bodyB);
            bThenA_A = await PostAsync(client, bodyA);
        }

        Assert.Equal(aThenB_A, bThenA_A);                                   // D-20, D-28
        Assert.Equal(aThenB_B, bThenA_B);                                   // D-20, D-28
    }

    [Fact]
    [Trait("TestCaseId", "TC-DET-004")]
    [Trait("Category", "DET")]
    public async Task DifferentContent_ProducesDifferentText()
    {
        using var factory = new OcrAclFactory();
        using var client = factory.CreateClient();

        var bodyOne = ContractSamples.ValidRequest.Replace(
            "\"SGVsbG8gd29ybGQ=\"", "\"SGVsbG8gd29ybGQx\"", StringComparison.Ordinal);

        var first = await PostAsync(client, ContractSamples.ValidRequest);
        var second = await PostAsync(client, bodyOne);

        using var jsonFirst = JsonDocument.Parse(first);
        using var jsonSecond = JsonDocument.Parse(second);
        var firstText = jsonFirst.RootElement.GetProperty("text").GetString();
        var secondText = jsonSecond.RootElement.GetProperty("text").GetString();

        Assert.NotEqual(firstText, secondText);                             // D-20
    }
}
