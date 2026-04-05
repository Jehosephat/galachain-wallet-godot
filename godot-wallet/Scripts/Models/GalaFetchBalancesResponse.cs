using System.Collections.Generic;
using System.Text.Json.Serialization;

// replace with FetchBalancesWithTokenMetadataResponse structure
public class GalaFetchBalancesResponse
{
	[JsonPropertyName("Data")]
	public List<GalaBalanceDto> Data { get; set; } = new();
}
