using System.Text.Json.Serialization;

namespace GalaWallet.Models;

// replace with FetchBalancesWithTokenMetadataResponse structure
public class GalaFetchBalancesRequest
{
	[JsonPropertyName("owner")]
	public string Owner { get; set; } = "";
}
