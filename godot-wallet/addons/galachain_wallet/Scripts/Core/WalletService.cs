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
	private readonly DtoPolicyRegistry _policyRegistry;

	public WalletService(
		PasswordCryptoService? crypto = null,
		IWalletStorage? storage = null,
		IGalaChainClient? galaChainClient = null,
		IGalaTransferClient? galaTransferClient = null,
		GalaSigner? galaSigner = null,
		DtoPolicyRegistry? policyRegistry = null)
	{
		_crypto = crypto ?? new PasswordCryptoService();
		_storage = storage ?? new FileWalletStorage();
		_galaChainClient = galaChainClient ?? new GalaChainClient();
		_galaTransferClient = galaTransferClient ?? new GalaTransferClient();
		_galaSigner = galaSigner ?? new GalaSigner();
		_policyRegistry = policyRegistry ?? new DtoPolicyRegistry();
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

			if (payload.SecretType == WalletSecretType.Mnemonic)
			{
				_state.Mnemonic = payload.Secret;
				var wallet = new Wallet(payload.Secret, "");
				_state.PrivateKey = NormalizePrivateKey(wallet.GetAccount(0).PrivateKey);
			}
			else
			{
				_state.Mnemonic = "";
				_state.PrivateKey = payload.Secret;
			}

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
	
	public async Task<NetworkResult<List<TokenBalanceModel>>> RefreshBalancesAsync()
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
		{
			_state.Balances = new List<TokenBalanceModel>();
			return NetworkResult<List<TokenBalanceModel>>.Success(_state.Balances);
		}

		var result = await _galaChainClient.FetchBalancesAsync(_state.Address);
		if (result.IsSuccess)
			_state.Balances = result.Data;

		return result;
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
	
	public ValidationResult ValidateTransfer(TransferDraft draft, decimal availableBalance)
	{
		var context = new TransactionContext
		{
			FromAddress = ToGalaAlias(_state.Address),
			ToAddress = draft.ToAddress,
			Quantity = draft.Quantity,
			AvailableBalance = availableBalance
		};

		return _policyRegistry.Validate("TransferToken", context);
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
	
	public async Task<NetworkResult<TransferPreviewResult>> PreviewTransferAsync(TransferDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<TransferPreviewResult>.Rejected("Wallet must be unlocked to preview transfer.");

		var request = BuildTransferRequest(draft);
		return await _galaChainClient.DryRunTransferAsync(request);
	}

	public async Task<NetworkResult<string>> SubmitTransferAsync(TransferDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<string>.Rejected("Wallet must be unlocked to transfer.");

		if (string.IsNullOrWhiteSpace(_state.PrivateKey))
			return NetworkResult<string>.Rejected("Private key is not available in memory.");

		var request = BuildTransferRequest(draft);
		_galaSigner.SignTransfer(request, _state.PrivateKey);

		var result = await _galaTransferClient.TransferAsync(request, ToGalaAlias(_state.Address));
		if (result.IsSuccess)
			await RefreshBalancesAsync();

		return result;
	}

	public ValidationResult ValidateBurn(BurnDraft draft, decimal availableBalance)
	{
		var context = new TransactionContext
		{
			FromAddress = ToGalaAlias(_state.Address),
			Quantity = draft.Quantity,
			AvailableBalance = availableBalance
		};

		return _policyRegistry.Validate("BurnTokens", context);
	}

	private GalaBurnTokensRequest BuildBurnRequest(BurnDraft draft)
	{
		long expiresAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds();

		return new GalaBurnTokensRequest
		{
			tokenInstances = new List<BurnTokenQuantity>
			{
				new BurnTokenQuantity
				{
					tokenInstanceKey = draft.TokenInstance,
					quantity = draft.Quantity
				}
			},
			uniqueKey = $"godot-wallet-{System.Guid.NewGuid()}",
			dtoExpiresAt = expiresAt
		};
	}

	public async Task<NetworkResult<TransferPreviewResult>> PreviewBurnAsync(BurnDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<TransferPreviewResult>.Rejected("Wallet must be unlocked to preview burn.");

		var request = BuildBurnRequest(draft);
		return await _galaChainClient.DryRunBurnAsync(request, ToGalaAlias(_state.Address));
	}

	public async Task<NetworkResult<string>> SubmitBurnAsync(BurnDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<string>.Rejected("Wallet must be unlocked to burn.");

		if (string.IsNullOrWhiteSpace(_state.PrivateKey))
			return NetworkResult<string>.Rejected("Private key is not available in memory.");

		var request = BuildBurnRequest(draft);
		_galaSigner.SignBurn(request, _state.PrivateKey);

		var result = await _galaTransferClient.BurnTokensAsync(request);
		if (result.IsSuccess)
			await RefreshBalancesAsync();

		return result;
	}

	public ValidationResult ValidateGrantAllowance(GrantAllowanceDraft draft, decimal availableBalance)
	{
		if (draft.Spenders.Count == 0)
			return ValidationResult.Fail("At least one spender is required.");

		foreach (var spender in draft.Spenders)
		{
			var context = new TransactionContext
			{
				FromAddress = ToGalaAlias(_state.Address),
				ToAddress = spender.User,
				Quantity = spender.Quantity,
				AvailableBalance = availableBalance,
				AllowanceType = (int)draft.AllowanceType,
				ExpiresUnixMs = draft.ExpiresUnixMs
			};

			var result = _policyRegistry.Validate("GrantAllowance", context);
			if (!result.IsValid)
				return result;
		}

		return ValidationResult.Ok();
	}

	private GalaGrantAllowanceRequest BuildGrantAllowanceRequest(GrantAllowanceDraft draft)
	{
		long expiresAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds();

		var quantities = new List<GrantAllowanceQuantity>();
		foreach (var spender in draft.Spenders)
		{
			quantities.Add(new GrantAllowanceQuantity
			{
				user = spender.User,
				quantity = spender.Quantity
			});
		}

		// Default uses to the quantity — a common and permissive default that
		// avoids under-granting when the game doesn't care about usage limits.
		string uses = !string.IsNullOrWhiteSpace(draft.Uses)
			? draft.Uses
			: (draft.Spenders.Count > 0 ? draft.Spenders[0].Quantity : "1");

		return new GalaGrantAllowanceRequest
		{
			allowanceType = (int)draft.AllowanceType,
			dtoExpiresAt = expiresAt,
			expires = draft.ExpiresUnixMs,
			quantities = quantities,
			tokenInstance = draft.TokenInstance,
			uniqueKey = $"godot-wallet-{System.Guid.NewGuid()}",
			uses = uses
		};
	}

	public async Task<NetworkResult<TransferPreviewResult>> PreviewGrantAllowanceAsync(GrantAllowanceDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<TransferPreviewResult>.Rejected("Wallet must be unlocked to preview allowance grant.");

		var request = BuildGrantAllowanceRequest(draft);
		return await _galaChainClient.DryRunGrantAllowanceAsync(request, ToGalaAlias(_state.Address));
	}

	public async Task<NetworkResult<string>> SubmitGrantAllowanceAsync(GrantAllowanceDraft draft)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			return NetworkResult<string>.Rejected("Wallet must be unlocked to grant an allowance.");

		if (string.IsNullOrWhiteSpace(_state.PrivateKey))
			return NetworkResult<string>.Rejected("Private key is not available in memory.");

		var request = BuildGrantAllowanceRequest(draft);
		_galaSigner.SignGrantAllowance(request, _state.PrivateKey);

		var result = await _galaTransferClient.GrantAllowanceAsync(request);
		if (result.IsSuccess)
			await RefreshBalancesAsync();

		return result;
	}

	public string SignMessage(string message)
	{
		if (!_state.HasWallet || !_state.IsUnlocked)
			throw new InvalidOperationException("Wallet must be unlocked to sign a message.");

		if (string.IsNullOrWhiteSpace(_state.PrivateKey))
			throw new InvalidOperationException("Private key is not available in memory.");

		return _galaSigner.SignMessage(message, _state.PrivateKey);
	}
}
