using System.Text;
using GalaWallet.Core;
using GalaWallet.Models;
using Nethereum.Signer;
using Nethereum.Util;

namespace GalaWallet.Tests;

/// <summary>
/// Golden vector tests verified against real GalaChain mainnet transaction:
/// Block 9627175, TX 50fef5611bda2f4187069b4fb803887fb67dc1f4d366bcb1e4b028d1673c8091
/// Source: https://explorer.galachain.com/details/asset-channel/9627175
/// </summary>
public class GoldenVectorTests
{
    // Exact DTO from the on-chain transaction (block 9627175)
    private static object BuildMainnetDto()
    {
        return new
        {
            uniqueKey = "galaconnect-operation-120d1136-4b02-4a39-8948-4fbafd06f948",
            from = "client|62693184c8e77046f7bd5878",
            to = "client|6501eebddfc1673847353ca3",
            quantity = "1",
            tokenInstance = new GalaTokenInstance
            {
                collection = "TownStar",
                category = "Building",
                type = "FruitsGreenhouse",
                additionalKey = "Epic",
                instance = "157"
            },
            // These should be excluded by canonical serialization
            signature = "d642ef1325cc83bebfab35120563f4fdc125a05c2b3fc76b3902c38ff775694221c12e07b59f0c9cfff1fa8a153573e08195255781984f373c81aa17a78ec13f1b",
            trace = new
            {
                traceId = "6439479214031654817",
                spanId = "5136217747196618473"
            }
        };
    }

    // Expected canonical JSON: keys sorted alphabetically, signature and trace excluded
    private const string ExpectedCanonicalJson =
        "{\"from\":\"client|62693184c8e77046f7bd5878\","
        + "\"quantity\":\"1\","
        + "\"to\":\"client|6501eebddfc1673847353ca3\","
        + "\"tokenInstance\":{\"additionalKey\":\"Epic\",\"category\":\"Building\",\"collection\":\"TownStar\",\"instance\":\"157\",\"type\":\"FruitsGreenhouse\"},"
        + "\"uniqueKey\":\"galaconnect-operation-120d1136-4b02-4a39-8948-4fbafd06f948\"}";

    // Signature from the on-chain transaction
    private const string OnChainSignature =
        "d642ef1325cc83bebfab35120563f4fdc125a05c2b3fc76b3902c38ff775694221c12e07b59f0c9cfff1fa8a153573e08195255781984f373c81aa17a78ec13f1b";

    // Ethereum address of the signer (from GCUP read set in the transaction)
    private const string SignerEthAddress = "0xcbf9a4A8b541177CD762d61f561Be4aF65561677";

    [Fact]
    public void CanonicalJson_MatchesExpectedOutput()
    {
        var dto = BuildMainnetDto();
        string json = GalaCanonicalJson.Serialize(dto);

        Assert.Equal(ExpectedCanonicalJson, json);
    }

    [Fact]
    public void CanonicalJson_ExcludesSignatureAndTrace()
    {
        var dto = BuildMainnetDto();
        string json = GalaCanonicalJson.Serialize(dto);

        Assert.DoesNotContain("signature", json);
        Assert.DoesNotContain("trace", json);
        Assert.DoesNotContain("traceId", json);
        Assert.DoesNotContain("spanId", json);
    }

    [Fact]
    public void Keccak256Hash_IsConsistentWithSignature()
    {
        // Compute the hash the same way GalaSigner does
        string canonicalJson = GalaCanonicalJson.Serialize(BuildMainnetDto());
        byte[] hash = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(canonicalJson));

        // Parse r, s, v from the compact 65-byte signature
        byte[] sigBytes = Convert.FromHexString(OnChainSignature);
        byte[] r = sigBytes[..32];
        byte[] s = sigBytes[32..64];
        byte v = sigBytes[64];

        var sig = EthECDSASignatureFactory.FromComponents(r, s, new[] { v });
        var recoveredKey = EthECKey.RecoverFromSignature(sig, hash);
        string recoveredAddress = recoveredKey.GetPublicAddress();

        // The recovered address should match the signer from the transaction
        Assert.Equal(
            SignerEthAddress.ToLowerInvariant(),
            recoveredAddress.ToLowerInvariant());
    }

    [Fact]
    public void CanonicalJson_KeyOrder_MatchesGalaChainExpectation()
    {
        string json = GalaCanonicalJson.Serialize(BuildMainnetDto());

        // Verify alphabetical key ordering at root level
        int fromIdx = json.IndexOf("\"from\"");
        int quantityIdx = json.IndexOf("\"quantity\"");
        int toIdx = json.IndexOf("\"to\"");
        int tokenIdx = json.IndexOf("\"tokenInstance\"");
        int uniqueIdx = json.IndexOf("\"uniqueKey\"");

        Assert.True(fromIdx < quantityIdx);
        Assert.True(quantityIdx < toIdx);
        Assert.True(toIdx < tokenIdx);
        Assert.True(tokenIdx < uniqueIdx);
    }

    [Fact]
    public void CanonicalJson_NestedKeyOrder_MatchesGalaChainExpectation()
    {
        string json = GalaCanonicalJson.Serialize(BuildMainnetDto());

        // Verify alphabetical key ordering within tokenInstance
        int addIdx = json.IndexOf("\"additionalKey\"");
        int catIdx = json.IndexOf("\"category\"");
        int colIdx = json.IndexOf("\"collection\"");
        int instIdx = json.IndexOf("\"instance\"");
        int typeIdx = json.IndexOf("\"type\"");

        Assert.True(addIdx < catIdx);
        Assert.True(catIdx < colIdx);
        Assert.True(colIdx < instIdx);
        Assert.True(instIdx < typeIdx);
    }
}
