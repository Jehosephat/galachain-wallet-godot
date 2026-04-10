using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GalaWallet.Models;

public class MintTokenResult
{
	public bool Success { get; set; }
	public string Message { get; set; } = "";
	public List<MintedTokenInstance> MintedInstances { get; set; } = new();
}

public class MintedTokenInstance
{
	[JsonPropertyName("collection")]
	public string Collection { get; set; } = "";

	[JsonPropertyName("category")]
	public string Category { get; set; } = "";

	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("additionalKey")]
	public string AdditionalKey { get; set; } = "";

	[JsonPropertyName("instance")]
	public string Instance { get; set; } = "";
}
