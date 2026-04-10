using System.Collections.Generic;

namespace GalaWallet.Models;

public class GalaBurnTokensRequest
{
	public List<BurnTokenQuantity> tokenInstances { get; set; } = new();
	public string uniqueKey { get; set; } = "";
	public long dtoExpiresAt { get; set; }
	public string signature { get; set; } = "";
}

public class BurnTokenQuantity
{
	public GalaTokenInstance tokenInstanceKey { get; set; } = new();
	public string quantity { get; set; } = "";
}
