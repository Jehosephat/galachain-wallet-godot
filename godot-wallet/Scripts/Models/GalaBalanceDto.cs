using System.Collections.Generic;
using System.Text.Json.Serialization;

// replace with FetchBalancesWithTokenMetadataResponse structure
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
