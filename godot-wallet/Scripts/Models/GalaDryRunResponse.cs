using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GalaWallet.Models;

public class GalaDryRunResponse
{
	[JsonPropertyName("Status")]
	public int Status { get; set; }

	[JsonPropertyName("Message")]
	public string Message { get; set; } = "";

	[JsonPropertyName("Data")]
	public GalaDryRunData Data { get; set; } = new();
}

public class GalaDryRunData
{
	[JsonPropertyName("response")]
	public GalaDryRunInnerResponse Response { get; set; } = new();

	[JsonPropertyName("writes")]
	public Dictionary<string, string> Writes { get; set; } = new();
}

public class GalaDryRunInnerResponse
{
	[JsonPropertyName("Status")]
	public int Status { get; set; }

	[JsonPropertyName("Message")]
	public string Message { get; set; } = "";

	[JsonPropertyName("ErrorCode")]
	public int ErrorCode { get; set; }
}
