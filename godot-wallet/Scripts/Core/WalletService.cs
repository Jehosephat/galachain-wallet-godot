using System;
using System.Collections.Generic;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;

public class WalletService : IWalletService
{
	private readonly WalletState _state = new();

	public bool HasWallet() => _state.HasWallet;
	public bool IsUnlocked() => _state.IsUnlocked;
	public string GetAddress() => _state.Address;

	public void CreateWallet()
	{
		var wallet = new Wallet(Wordlist.English, WordCount.Twelve);
		var account = wallet.GetAccount(0);
		var mnemonic = string.Join(" ", wallet.Words);

		_state.HasWallet = true;
		_state.IsUnlocked = true;

		_state.Mnemonic = mnemonic;
		_state.PendingRecoveryPhrase = mnemonic;

		_state.PrivateKey = NormalizePrivateKey(account.PrivateKey);
		_state.Address = account.Address;

		_state.Balances = new List<TokenBalanceModel>
		{
			new TokenBalanceModel { Symbol = "GALA", DisplayAmount = "125.00" },
			new TokenBalanceModel { Symbol = "TREZ", DisplayAmount = "42.00" }
		};
	}

	public void ImportPrivateKey(string privateKey)
	{
		var normalizedPrivateKey = NormalizePrivateKey(privateKey);
		var account = new Account(normalizedPrivateKey);

		_state.HasWallet = true;
		_state.IsUnlocked = true;

		_state.Mnemonic = "";
		_state.PendingRecoveryPhrase = "";

		_state.PrivateKey = normalizedPrivateKey;
		_state.Address = account.Address;

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

	public string ConsumePendingRecoveryPhrase()
	{
		var phrase = _state.PendingRecoveryPhrase;
		_state.PendingRecoveryPhrase = "";
		return phrase;
	}

	private static string NormalizePrivateKey(string privateKey)
	{
		var trimmed = privateKey.Trim();

		if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			trimmed = "0x" + trimmed;
		}

		return trimmed;
	}
}
