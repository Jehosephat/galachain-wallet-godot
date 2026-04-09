using System.Text.Json.Serialization;

namespace GalaWallet.Models;

public class GalaChainResponse
{
	[JsonPropertyName("Status")]
	public int Status { get; set; }

	[JsonPropertyName("Message")]
	public string Message { get; set; } = "";
}
