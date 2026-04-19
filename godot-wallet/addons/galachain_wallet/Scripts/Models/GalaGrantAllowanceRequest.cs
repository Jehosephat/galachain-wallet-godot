using System.Collections.Generic;

namespace GalaWallet.Models;

public class GalaGrantAllowanceRequest
{
	public int allowanceType { get; set; }
	public long dtoExpiresAt { get; set; }
	public long expires { get; set; }
	public List<GrantAllowanceQuantity> quantities { get; set; } = new();
	public GalaTokenInstance tokenInstance { get; set; } = new();
	public string uniqueKey { get; set; } = "";
	public string uses { get; set; } = "";
	public string signature { get; set; } = "";
}

public class GrantAllowanceQuantity
{
	public string user { get; set; } = "";
	public string quantity { get; set; } = "";
}
