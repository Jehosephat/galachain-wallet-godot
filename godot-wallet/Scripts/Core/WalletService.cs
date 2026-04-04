using System.Collections.Generic;

public class WalletService : IWalletService
{
	private readonly WalletState _state = new();

	public bool HasWallet() => _state.HasWallet;
	public bool IsUnlocked() => _state.IsUnlocked;
	public string GetAddress() => _state.Address;

	public void CreateWallet()
	{
		_state.HasWallet = true;
		_state.IsUnlocked = true;
		_state.Address = "0x1234567890abcdef1234567890abcdef12345678";
		_state.Balances = new List<TokenBalanceModel>
		{
			new TokenBalanceModel { Symbol = "GALA", DisplayAmount = "125.00" },
			new TokenBalanceModel { Symbol = "TREZ", DisplayAmount = "42.00" }
		};
	}

	public void ImportPrivateKey(string privateKey)
	{
		_state.HasWallet = true;
		_state.IsUnlocked = true;
		_state.Address = "0ximported00000000000000000000000000000000";
		_state.Balances = new List<TokenBalanceModel>();
	}

	public bool Unlock(string password)
	{
		if (!_state.HasWallet) return false;
		_state.IsUnlocked = true;
		return true;
	}

	public void Lock()
	{
		_state.IsUnlocked = false;
	}

	public List<TokenBalanceModel> GetBalances()
	{
		return _state.Balances;
	}
}
