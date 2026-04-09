using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GalaWallet.Models;

public class GalaFetchBalancesResponse
{
	[JsonPropertyName("Data")]
	public GalaFetchBalancesData Data { get; set; } = new();
}

public class GalaFetchBalancesData
{
	[JsonPropertyName("results")]
	public List<GalaBalanceWithMetadata> Results { get; set; } = new();

	[JsonPropertyName("nextPageBookmark")]
	public string NextPageBookmark { get; set; } = "";
}

public class GalaBalanceWithMetadata
{
	[JsonPropertyName("balance")]
	public GalaBalanceDto Balance { get; set; } = new();

	[JsonPropertyName("token")]
	public GalaTokenMetadata Token { get; set; } = new();
}
