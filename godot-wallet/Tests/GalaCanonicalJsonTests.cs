using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.Tests;

public class GalaCanonicalJsonTests
{
    [Fact]
    public void Serialize_SortsKeysAlphabetically()
    {
        var obj = new { zebra = "z", apple = "a", mango = "m" };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Equal("{\"apple\":\"a\",\"mango\":\"m\",\"zebra\":\"z\"}", json);
    }

    [Fact]
    public void Serialize_SortsNestedObjectKeys()
    {
        var obj = new { outer = new { beta = 2, alpha = 1 } };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Equal("{\"outer\":{\"alpha\":1,\"beta\":2}}", json);
    }

    [Fact]
    public void Serialize_ExcludesSignatureAtRootLevel()
    {
        var obj = new { from = "alice", signature = "0xabc", to = "bob" };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.DoesNotContain("signature", json);
        Assert.Contains("from", json);
        Assert.Contains("to", json);
    }

    [Fact]
    public void Serialize_ExcludesTraceAtRootLevel()
    {
        var obj = new { amount = "10", trace = "some-trace-id" };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.DoesNotContain("trace", json);
        Assert.Contains("amount", json);
    }

    [Fact]
    public void Serialize_PreservesSignatureInNestedObjects()
    {
        var obj = new { data = new { signature = "keep-this" } };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Contains("signature", json);
    }

    [Fact]
    public void Serialize_CamelCasesPropertyNames()
    {
        var obj = new { FirstName = "Alice", LastName = "Bob" };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Contains("firstName", json);
        Assert.Contains("lastName", json);
        Assert.DoesNotContain("FirstName", json);
    }

    [Fact]
    public void Serialize_HandlesArrays()
    {
        var obj = new { items = new[] { "c", "a", "b" } };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Equal("{\"items\":[\"c\",\"a\",\"b\"]}", json);
    }

    [Fact]
    public void Serialize_HandlesNullValues()
    {
        var obj = new { name = (string?)null, value = "ok" };
        string json = GalaCanonicalJson.Serialize(obj);

        Assert.Contains("null", json);
        Assert.Contains("\"ok\"", json);
    }

    [Fact]
    public void Serialize_TransferTokenRequest_ProducesDeterministicOutput()
    {
        var request = new
        {
            from = "eth|abc123",
            to = "client|def456",
            tokenInstance = new GalaTokenInstance
            {
                collection = "GALA",
                category = "Unit",
                type = "none",
                additionalKey = "none",
                instance = "0"
            },
            quantity = "15",
            uniqueKey = "test-key-001",
            dtoExpiresAt = 1700000000000L
        };

        string json1 = GalaCanonicalJson.Serialize(request);
        string json2 = GalaCanonicalJson.Serialize(request);

        Assert.Equal(json1, json2);
    }

    [Fact]
    public void Serialize_TransferTokenRequest_SortsAllFieldsCorrectly()
    {
        var request = new
        {
            from = "eth|abc",
            to = "client|def",
            tokenInstance = new GalaTokenInstance
            {
                collection = "GALA",
                category = "Unit",
                type = "none",
                additionalKey = "none",
                instance = "0"
            },
            quantity = "10",
            uniqueKey = "key1",
            dtoExpiresAt = 999L
        };

        string json = GalaCanonicalJson.Serialize(request);

        // Verify top-level key order: dtoExpiresAt, from, quantity, to, tokenInstance, uniqueKey
        int dtoIdx = json.IndexOf("\"dtoExpiresAt\"");
        int fromIdx = json.IndexOf("\"from\"");
        int quantityIdx = json.IndexOf("\"quantity\"");
        int toIdx = json.IndexOf("\"to\"");
        int tokenIdx = json.IndexOf("\"tokenInstance\"");
        int uniqueIdx = json.IndexOf("\"uniqueKey\"");

        Assert.True(dtoIdx < fromIdx, "dtoExpiresAt should come before from");
        Assert.True(fromIdx < quantityIdx, "from should come before quantity");
        Assert.True(quantityIdx < toIdx, "quantity should come before to");
        Assert.True(toIdx < tokenIdx, "to should come before tokenInstance");
        Assert.True(tokenIdx < uniqueIdx, "tokenInstance should come before uniqueKey");
    }
}
