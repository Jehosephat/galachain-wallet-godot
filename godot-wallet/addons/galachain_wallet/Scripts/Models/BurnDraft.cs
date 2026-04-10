namespace GalaWallet.Models;

public class BurnDraft
{
	public string Quantity { get; set; } = "";
	public GalaTokenInstance TokenInstance { get; set; } = new();
	public string DisplaySymbol { get; set; } = "";
}
