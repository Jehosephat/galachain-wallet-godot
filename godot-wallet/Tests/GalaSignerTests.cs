using GalaWallet.Core;
using GalaWallet.Models;
using Nethereum.Signer;

namespace GalaWallet.Tests;

public class GalaSignerTests
{
    // Deterministic test key (DO NOT use in production)
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f703c9e48b76a8e324";

    private GalaTransferTokenRequest BuildTestRequest()
    {
        return new GalaTransferTokenRequest
        {
            from = "eth|2c7536E3605D9C16a7a3D7b1898e529396a65c23",
            to = "client|5f58d8641586e117c5e68834",
            tokenInstance = new GalaTokenInstance
            {
                collection = "GALA",
                category = "Unit",
                type = "none",
                additionalKey = "none",
                instance = "0"
            },
            quantity = "10",
            uniqueKey = "test-unique-key-001",
            dtoExpiresAt = 1700000000000L
        };
    }

    [Fact]
    public void SignTransfer_PopulatesSignatureField()
    {
        var signer = new GalaSigner();
        var request = BuildTestRequest();

        Assert.Equal("", request.signature);

        signer.SignTransfer(request, TestPrivateKey);

        Assert.False(string.IsNullOrWhiteSpace(request.signature));
        Assert.StartsWith("0x", request.signature);
    }

    [Fact]
    public void SignTransfer_ProducesDeterministicSignature()
    {
        var signer = new GalaSigner();

        var request1 = BuildTestRequest();
        signer.SignTransfer(request1, TestPrivateKey);

        var request2 = BuildTestRequest();
        signer.SignTransfer(request2, TestPrivateKey);

        Assert.Equal(request1.signature, request2.signature);
    }

    [Fact]
    public void SignTransfer_DifferentDataProducesDifferentSignature()
    {
        var signer = new GalaSigner();

        var request1 = BuildTestRequest();
        signer.SignTransfer(request1, TestPrivateKey);

        var request2 = BuildTestRequest();
        request2.quantity = "20";
        signer.SignTransfer(request2, TestPrivateKey);

        Assert.NotEqual(request1.signature, request2.signature);
    }

    [Fact]
    public void SignTransfer_SignatureHasCorrectLength()
    {
        var signer = new GalaSigner();
        var request = BuildTestRequest();

        signer.SignTransfer(request, TestPrivateKey);

        // Ethereum signature: 0x + 130 hex chars (65 bytes: r=32, s=32, v=1)
        Assert.Equal(132, request.signature.Length);
    }

    [Fact]
    public void SignTransfer_SameInputAlwaysProducesSameHash()
    {
        // Verifies that canonical serialization is stable — same DTO yields same signature
        var signer = new GalaSigner();

        var request1 = BuildTestRequest();
        var request2 = BuildTestRequest();

        signer.SignTransfer(request1, TestPrivateKey);
        signer.SignTransfer(request2, TestPrivateKey);

        // If the canonical JSON or hash changed between calls, signatures would differ
        Assert.Equal(request1.signature, request2.signature);
    }
}
