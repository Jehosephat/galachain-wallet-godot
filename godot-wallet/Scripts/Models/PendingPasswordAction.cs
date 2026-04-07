namespace GalaWallet.Models;

public enum PendingPasswordAction
{
	None = 0,
	CreateWallet = 1,
	ImportWallet = 2,
	UnlockWallet = 3,
	ImportMnemonic= 4
}
