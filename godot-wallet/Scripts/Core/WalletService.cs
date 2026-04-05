using System;
using System.Collections.Generic;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;

public class WalletService : IWalletService
{
	private readonly WalletState _state = new();
	private readonly PasswordCryptoService _crypto = new();
	private readonly IWalletStorage _storage = new FileWalletStorage();

	public bool HasWallet() => _state.HasWallet;
	public bool IsUnlocked() => _state.IsUnlocked;
	public string GetAddress() => _state.Address;

	public void CreateWallet(string password)
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

		var record = _crypto.EncryptSecret(
			_state.Address,
			WalletSecretType.Mnemonic,
			mnemonic,
			password);

		_storage.Save(record);
	}

	public void ImportPrivateKey(string privateKey, string password)
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

		var record = _crypto.EncryptSecret(
			_state.Address,
			WalletSecretType.PrivateKey,
			normalizedPrivateKey,
			password);

		_storage.Save(record);
	}

	public bool Unlock(string password)
	{
		var record = _storage.Load();
		if (record == null)
			return false;

		try
		{
			var payload = _crypto.DecryptSecret(record, password);

			_state.HasWallet = true;
			_state.IsUnlocked = true;
			_state.Address = record.Address;
			_state.Mnemonic = payload.SecretType == WalletSecretType.Mnemonic ? payload.Secret : "";
			_state.PrivateKey = payload.SecretType == WalletSecretType.PrivateKey ? payload.Secret : "";

			return true;
		}
		catch
		{
			return false;
		}
	}

	public void Lock()
	{
		_state.IsUnlocked = false;
		_state.PrivateKey = "";
		_state.Mnemonic = "";
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
	
	public void LoadWalletMetadataIfPresent()
	{
		var record = _storage.Load();
		if (record == null)
			return;

		_state.HasWallet = true;
		_state.IsUnlocked = false;
		_state.Address = record.Address;
		_state.Mnemonic = "";
		_state.PrivateKey = "";
	}
}
