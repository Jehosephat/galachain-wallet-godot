using System.Collections.Generic;

namespace GalaWallet.Models;

public class GrantAllowanceDraft
{
	public AllowanceType AllowanceType { get; set; } = AllowanceType.Transfer;
	public List<GrantAllowanceSpender> Spenders { get; set; } = new();
	public string Uses { get; set; } = "";
	public long ExpiresUnixMs { get; set; }
	public GalaTokenInstance TokenInstance { get; set; } = new();
	public string DisplaySymbol { get; set; } = "";
}

public class GrantAllowanceSpender
{
	public string User { get; set; } = "";
	public string Quantity { get; set; } = "";
}
