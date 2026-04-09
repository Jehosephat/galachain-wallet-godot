using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GalaWallet.Models;

public class GalaBalanceDto
{
	[JsonPropertyName("collection")]
	public string Collection { get; set; } = "";

	[JsonPropertyName("category")]
	public string Category { get; set; } = "";

	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("additionalKey")]
	public string AdditionalKey { get; set; } = "";

	[JsonPropertyName("quantity")]
	public string Quantity { get; set; } = "0";

	[JsonPropertyName("lockedHolds")]
	public List<GalaLockedHoldDto> LockedHolds { get; set; } = new();
}

public class GalaLockedHoldDto
{
	[JsonPropertyName("quantity")]
	public string Quantity { get; set; } = "0";
}

public class GalaTokenMetadata
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("symbol")]
	public string Symbol { get; set; } = "";

	[JsonPropertyName("description")]
	public string Description { get; set; } = "";

	[JsonPropertyName("image")]
	public string Image { get; set; } = "";

	[JsonPropertyName("decimals")]
	public int Decimals { get; set; }

	[JsonPropertyName("isNonFungible")]
	public bool IsNonFungible { get; set; }
}
