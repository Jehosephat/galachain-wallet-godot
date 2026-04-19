namespace GalaWallet.Models;

/// <summary>
/// GalaChain allowance types. Values match the on-chain enum numeric codes.
/// Only Transfer and Burn are supported by this wallet — granting Mint/Swap/Lock/Spend
/// is out of scope (Mint in particular uses a separate GrantMintAllowance flow).
/// </summary>
public enum AllowanceType
{
	Use = 0,
	Lock = 1,
	Spend = 2,
	Transfer = 3,
	Mint = 4,
	Swap = 5,
	Burn = 6
}
