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
	public event Action? BalancesRefreshed;

	public WalletFacade(IWalletService? walletService = null)
	{
		_walletService = walletService ?? new WalletService();
		_walletService.LoadWalletMetadataIfPresent();
		_galaChainWalletScene = GD.Load<PackedScene>("res://addons/galachain_wallet/scenes/GalaChainWallet.tscn");
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

	private void SubscribeToWalletEvents(GalaChainWallet wallet)
	{
		wallet.WalletCreated += addr => WalletCreated?.Invoke(addr);
		wallet.WalletImported += addr => WalletImported?.Invoke(addr);
		wallet.WalletUnlocked += addr => WalletUnlocked?.Invoke(addr);
		wallet.WalletLocked += () => WalletLocked?.Invoke();
		wallet.TransferCompleted += (to, qty, sym) => TransferCompleted?.Invoke(to, qty, sym);
		wallet.TransferFailed += err => TransferFailed?.Invoke(err);
		wallet.BalancesRefreshed += () => BalancesRefreshed?.Invoke();
	}
}
