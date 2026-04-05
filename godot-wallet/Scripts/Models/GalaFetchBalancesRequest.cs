using System.Text.Json.Serialization;

// replace with FetchBalancesWithTokenMetadataResponse structure
public class GalaFetchBalancesRequest
{
	[JsonPropertyName("owner")]
	public string Owner { get; set; } = "";
}
