using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GalaWallet.Models;
using GalaWallet.UI;

namespace GalaWallet.Core;

public class WalletFacade
{
	private readonly IWalletService _walletService;
	private readonly PackedScene _galaChainWalletScene;
	private GalaChainWallet? _galaChainWallet;

	// Events — game code subscribes to these
	public event Action<string>? WalletCreated;
	public event Action<string>? WalletImported;
	public event Action<string>? WalletUnlocked;
	public event Action? WalletLocked;
	public event Action<string, string, string>? TransferCompleted;
	public event Action<string>? TransferFailed;
	public event Action<string, string>? BurnCompleted;
	public event Action<string>? BurnFailed;
	public event Action<string, string, string, string>? AllowanceGranted;
	public event Action<string>? AllowanceGrantFailed;
	public event Action<string, string, string>? MessageSigned;
	public event Action? MessageSignDeclined;
	public event Action? BalancesRefreshed;

	/// <summary>
	/// Creates a WalletFacade. Defaults to GalaChain mainnet.
	/// Pass a custom IWalletService for testing or dependency injection scenarios.
	/// </summary>
	public WalletFacade(IWalletService? walletService = null)
	{
		_walletService = walletService ?? new WalletService();
		_walletService.LoadWalletMetadataIfPresent();
		_galaChainWalletScene = GD.Load<PackedScene>("res://addons/galachain_wallet/scenes/GalaChainWallet.tscn");
	}

	/// <summary>
	/// Creates a WalletFacade configured for a specific GalaChain network.
	/// Use GalaChainNetworkConfig.Testnet() for testnet, or build a custom config
	/// with your own ApiBaseUrl, Channel, and Contract.
	/// </summary>
	public WalletFacade(GalaChainNetworkConfig config)
		: this(new WalletService(
			galaChainClient: new GalaChainClient(config),
			galaTransferClient: new GalaTransferClient(config)))
	{
	}

	public void OpenWallet(Control parent)
	{
		if (_galaChainWallet == null)
		{
			_galaChainWallet = _galaChainWalletScene.Instantiate<GalaChainWallet>();
			_galaChainWallet.Initialize(_walletService);
			SubscribeToWalletEvents(_galaChainWallet);
			parent.AddChild(_galaChainWallet);
		}

		_galaChainWallet.Visible = true;
	}

	public void CloseWallet()
	{
		if (_galaChainWallet != null)
		{
			_galaChainWallet.Visible = false;
		}
	}

	public bool HasWallet()
	{
		return _walletService.HasWallet();
	}

	public bool IsUnlocked()
	{
		return _walletService.IsUnlocked();
	}

	public string GetCurrentAddress()
	{
		return _walletService.GetAddress();
	}

	public List<TokenBalanceModel> GetBalances()
	{
		return _walletService.GetBalances();
	}

	public async Task RefreshBalancesAsync()
	{
		await _walletService.RefreshBalancesAsync();
	}

	public void RequestTransfer(string toAddress, string quantity, string tokenSymbol)
	{
		if (_galaChainWallet == null)
		{
			return;
		}

		_galaChainWallet.RequestTransfer(toAddress, quantity, tokenSymbol);
	}

	public void RequestBurn(string quantity, string tokenSymbol)
	{
		if (_galaChainWallet == null)
		{
			return;
		}

		_galaChainWallet.RequestBurn(quantity, tokenSymbol);
	}

	/// <summary>
	/// Asks the player to grant a Transfer or Burn allowance for a specific token.
	/// Opens a pre-filled confirmation dialog; if the wallet is locked, prompts unlock first.
	///
	/// Subscribe to AllowanceGranted for success or AllowanceGrantFailed for errors.
	/// </summary>
	public void RequestGrantAllowance(string spender, string quantity, string tokenSymbol, AllowanceType type, int expiresInDays = 0)
	{
		if (_galaChainWallet == null)
		{
			return;
		}

		_galaChainWallet.RequestGrantAllowance(spender, quantity, tokenSymbol, type, expiresInDays);
	}

	/// <summary>
	/// Asks the player to sign an arbitrary message (EIP-191 personal_sign).
	/// Used for authentication challenges — the game server issues a message,
	/// the player signs it, the server verifies the signature recovers to the
	/// claimed address.
	///
	/// Subscribe to MessageSigned to receive the signature and address, or
	/// MessageSignDeclined if the user cancels or the wallet is in a bad state.
	/// </summary>
	public void RequestSignMessage(string message)
	{
		if (_galaChainWallet == null)
		{
			return;
		}

		_galaChainWallet.RequestSignMessage(message);
	}

	private void SubscribeToWalletEvents(GalaChainWallet wallet)
	{
		wallet.WalletCreated += addr => WalletCreated?.Invoke(addr);
		wallet.WalletImported += addr => WalletImported?.Invoke(addr);
		wallet.WalletUnlocked += addr => WalletUnlocked?.Invoke(addr);
		wallet.WalletLocked += () => WalletLocked?.Invoke();
		wallet.TransferCompleted += (to, qty, sym) => TransferCompleted?.Invoke(to, qty, sym);
		wallet.TransferFailed += err => TransferFailed?.Invoke(err);
		wallet.BurnCompleted += (qty, sym) => BurnCompleted?.Invoke(qty, sym);
		wallet.BurnFailed += err => BurnFailed?.Invoke(err);
		wallet.AllowanceGranted += (spender, qty, sym, type) => AllowanceGranted?.Invoke(spender, qty, sym, type);
		wallet.AllowanceGrantFailed += err => AllowanceGrantFailed?.Invoke(err);
		wallet.MessageSigned += (msg, sig, addr) => MessageSigned?.Invoke(msg, sig, addr);
		wallet.MessageSignDeclined += () => MessageSignDeclined?.Invoke();
		wallet.BalancesRefreshed += () => BalancesRefreshed?.Invoke();
	}
}
