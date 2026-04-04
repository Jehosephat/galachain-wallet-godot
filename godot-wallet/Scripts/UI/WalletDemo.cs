using Godot;
using System.Text;
using System.Collections.Generic;

public partial class WalletDemo : Control
{
	private IWalletService _walletService = new WalletService();

	private Label _statusLabel = null!;
	private Label _addressValueLabel = null!;
	private ItemList _balancesList = null!;
	private RichTextLabel _logOutput = null!;

	public override void _Ready()
	{
		_statusLabel = GetNode<Label>("%StatusLabel");
		_addressValueLabel = GetNode<Label>("%AddressValueLabel");
		_balancesList = GetNode<ItemList>("%BalancesList");
		_logOutput = GetNode<RichTextLabel>("%LogOutput");

		GetNode<Button>("%CreateWalletButton").Pressed += OnCreateWalletPressed;
		GetNode<Button>("%ImportWalletButton").Pressed += OnImportWalletPressed;
		GetNode<Button>("%UnlockButton").Pressed += OnUnlockPressed;
		GetNode<Button>("%LockButton").Pressed += OnLockPressed;
		GetNode<Button>("%CopyAddressButton").Pressed += OnCopyAddressPressed;
		GetNode<Button>("%RefreshBalancesButton").Pressed += OnRefreshBalancesPressed;

		RefreshUi();
		Log("Wallet demo ready.");
	}

	private void OnCreateWalletPressed()
	{
		_walletService.CreateWallet();
		Log("Created test wallet.");
		RefreshUi();
	}

	private void OnImportWalletPressed()
	{
		_walletService.ImportPrivateKey("dummy-private-key");
		Log("Imported test wallet.");
		RefreshUi();
	}

	private void OnUnlockPressed()
	{
		bool ok = _walletService.Unlock("dummy-password");
		Log(ok ? "Wallet unlocked." : "Unlock failed.");
		RefreshUi();
	}

	private void OnLockPressed()
	{
		_walletService.Lock();
		Log("Wallet locked.");
		RefreshUi();
	}

	private void OnCopyAddressPressed()
	{
		string address = _walletService.GetAddress();
		if (string.IsNullOrWhiteSpace(address))
		{
			Log("No address to copy.");
			return;
		}

		DisplayServer.ClipboardSet(address);
		Log($"Copied address: {address}");
	}

	private void OnRefreshBalancesPressed()
	{
		Log("Refreshing balances.");
		RefreshUi();
	}

	private void RefreshUi()
	{
		_statusLabel.Text = BuildStatusText();
		_addressValueLabel.Text = _walletService.HasWallet()
			? _walletService.GetAddress()
			: "No wallet loaded";

		_balancesList.Clear();

		if (_walletService.HasWallet() && _walletService.IsUnlocked())
		{
			List<TokenBalanceModel> balances = _walletService.GetBalances();
			if (balances.Count == 0)
			{
				_balancesList.AddItem("No balances");
			}
			else
			{
				foreach (var balance in balances)
				{
					_balancesList.AddItem($"{balance.Symbol}: {balance.DisplayAmount}");
				}
			}
		}
		else
		{
			_balancesList.AddItem("Wallet locked or unavailable");
		}
	}

	private string BuildStatusText()
	{
		if (!_walletService.HasWallet())
			return "Status: No wallet";

		return _walletService.IsUnlocked()
			? "Status: Wallet unlocked"
			: "Status: Wallet locked";
	}

	private void Log(string message)
	{
		_logOutput.AppendText($"{message}\n");
	}
}
