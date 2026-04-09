using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.Tests;

public class GalaTransferClientTests
{
    [Fact]
    public void ParseTransferResponse_Status1_ReturnsSuccess()
    {
        string body = "{\"Status\":1,\"Message\":\"Success\",\"Data\":[]}";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.True(result.IsSuccess);
        Assert.Equal(body, result.Data);
    }

    [Fact]
    public void ParseTransferResponse_Status0_ReturnsRejected()
    {
        string body = "{\"Status\":0,\"Message\":\"Insufficient balance\"}";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(NetworkErrorKind.Rejected, result.ErrorKind);
        Assert.Equal("Insufficient balance", result.ErrorMessage);
    }

    [Fact]
    public void ParseTransferResponse_Status0_WithErrorPayload_ReturnsRejectedWithMessage()
    {
        string body = "{\"Status\":0,\"Message\":\"DTO validation failed: uniqueKey already exists\",\"ErrorCode\":409}";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.False(result.IsSuccess);
        Assert.Contains("uniqueKey already exists", result.ErrorMessage);
    }

    [Fact]
    public void ParseTransferResponse_InvalidJson_ReturnsParseError()
    {
        string body = "not json at all";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(NetworkErrorKind.ParseError, result.ErrorKind);
    }

    [Fact]
    public void ParseTransferResponse_EmptyObject_ReturnsRejected()
    {
        // Status defaults to 0 when missing
        string body = "{}";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(NetworkErrorKind.Rejected, result.ErrorKind);
    }

    [Fact]
    public void ParseTransferResponse_NullBody_ReturnsParseError()
    {
        string body = "null";

        var result = GalaTransferClient.ParseTransferResponse(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(NetworkErrorKind.ParseError, result.ErrorKind);
    }
}
