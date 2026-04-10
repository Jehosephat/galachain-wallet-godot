namespace GalaWallet.Models;

public class GalaMintTokenRequest
{
	public GalaTokenClassKey tokenClass { get; set; } = new();
	public string owner { get; set; } = "";
	public string quantity { get; set; } = "";
	public string uniqueKey { get; set; } = "";
	public long dtoExpiresAt { get; set; }
	public string signature { get; set; } = "";
}
