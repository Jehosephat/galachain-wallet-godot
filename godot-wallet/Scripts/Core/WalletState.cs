using System.Collections.Generic;

public class WalletState
{
	public bool HasWallet { get; set; }
	public bool IsUnlocked { get; set; }
	public string Address { get; set; } = "";
	public List<TokenBalanceModel> Balances { get; set; } = new();
}
