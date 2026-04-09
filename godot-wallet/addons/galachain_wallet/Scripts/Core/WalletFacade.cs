using Godot;
using System.Threading.Tasks;
using GalaWallet.Models;
using GalaWallet.UI;

namespace GalaWallet.Core;

public class WalletFacade
{
	private readonly IWalletService _walletService;
	private readonly PackedScene _galaChainWalletScene;
	private GalaChainWallet? _galaChainWallet;

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
}
