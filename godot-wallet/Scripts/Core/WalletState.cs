using System.Collections.Generic;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class WalletState
{
	public bool HasWallet { get; set; }
	public bool IsUnlocked { get; set; }
	public string Address { get; set; } = "";
	public string PrivateKey { get; set; } = "";
	public string Mnemonic { get; set; } = "";

	// For one-time display after wallet creation
	public string PendingRecoveryPhrase { get; set; } = "";
	public List<TokenBalanceModel> Balances { get; set; } = new();
}
