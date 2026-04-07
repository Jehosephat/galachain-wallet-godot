using System;
using System.Collections.Generic;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class WalletService : IWalletService
{
	private readonly WalletState _state = new();
	private readonly PasswordCryptoService _crypto;
	private readonly IWalletStorage _storage;
	private readonly IGalaChainClient _galaChainClient;
	private readonly IGalaTransferClient _galaTransferClient;
	private readonly GalaSigner _galaSigner;

	public WalletService(
		PasswordCryptoService? crypto = null,
		IWalletStorage? storage = null,
		IGalaChainClient? galaChainClient = null,
		IGalaTransferClient? galaTransferClient = null,
		GalaSigner? galaSigner = null)
	{
		_crypto = crypto ?? new PasswordCryptoService();
		_storage = storage ?? new FileWalletStorage();
		_galaChainClient = galaChainClient ?? new GalaChainClient();
		_galaTransferClient = galaTransferClient ?? new GalaTransferClient();
		_galaSigner = galaSigner ?? new GalaSigner();
	}

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

		var record = _crypto.EncryptSecret(
			_state.Address,
			WalletSecretType.PrivateKey,
			normalizedPrivateKey,
			password);

		_storage.Save(record);
	}
	
	public void ImportMnemonic(string mnemonic, string password)
	{
		string normalizedMnemonic = NormalizeMnemonic(mnemonic);

		var wallet = new Nethereum.HdWallet.Wallet(normalizedMnemonic, "");
		var account = wallet.GetAccount(0);

		_state.HasWallet = true;
		_state.IsUnlocked = true;
		_state.Mnemonic = normalizedMnemonic;
		_state.PendingRecoveryPhrase = "";
		_state.PrivateKey = NormalizePrivateKey(account.PrivateKey);
		_state.Address = account.Address;

		var record = _crypto.EncryptSecret(
			_state.Address,
			WalletSecretType.Mnemonic,
			normalizedMnemonic,
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
	
	public async Task RefreshBalancesAsync()
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
		{
			_state.Balances = new List<TokenBalanceModel>();
			return;
		}

		_state.Balances = await _galaChainClient.FetchBalancesAsync(_state.Address);
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
	
	private static string NormalizeMnemonic(string mnemonic)
	{
		return string.Join(
			" ",
			mnemonic
				.Trim()
				.ToLowerInvariant()
				.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries)
		);
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
	
	private static string ToGalaAlias(string ethAddress)
	{
		string trimmed = ethAddress.Trim();

		if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Expected 0x-prefixed address.");

		return $"eth|{trimmed[2..]}";
	}
	
	private GalaTransferTokenRequest BuildTransferRequest(TransferDraft draft)
	{
		long expiresAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds();

		return new GalaTransferTokenRequest
		{
			from = ToGalaAlias(_state.Address),
			to = draft.ToAddress, //validation should force this to eth| or client|
			quantity = draft.Quantity,
			tokenInstance = draft.TokenInstance,
			uniqueKey = $"godot-wallet-{System.Guid.NewGuid()}",
			dtoExpiresAt = expiresAt
		};
	}
	
	public async Task SubmitTransferAsync(TransferDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			throw new InvalidOperationException("Wallet must be unlocked to transfer.");

		if (string.IsNullOrWhiteSpace(_state.PrivateKey))
			throw new InvalidOperationException("Private key is not available in memory.");

		var request = BuildTransferRequest(draft);

		_galaSigner.SignTransfer(request, _state.PrivateKey);
		await _galaTransferClient.TransferAsync(request, ToGalaAlias(_state.Address));
		await RefreshBalancesAsync();
	}
}
