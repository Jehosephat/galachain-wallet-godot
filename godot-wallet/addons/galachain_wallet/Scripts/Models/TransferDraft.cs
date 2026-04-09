namespace GalaWallet.Models;

public class TransferDraft
{
	public string ToAddress { get; set; } = "";
	public string Quantity { get; set; } = "";
	public GalaTokenInstance TokenInstance { get; set; } = new();

	public string DisplaySymbol { get; set; } = "";
}
