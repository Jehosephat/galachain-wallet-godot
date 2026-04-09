namespace GalaWallet.Models;

public class TokenBalanceModel
{
	public string Symbol { get; set; } = "";
	public string DisplayAmount { get; set; } = "";

	public string Collection { get; set; } = "";
	public string Category { get; set; } = "";
	public string Type { get; set; } = "";
	public string AdditionalKey { get; set; } = "";
	public string Instance { get; set; } = "0";

	public string RawQuantity { get; set; } = "0";
	public decimal AvailableAmount { get; set; }
}
