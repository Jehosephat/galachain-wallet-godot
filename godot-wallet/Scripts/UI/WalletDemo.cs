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
	private Button _importWalletButton = null!;
	private Button _unlockButton = null!;
	private Button _lockButton = null!;
	private Button _copyAddressButton = null!;
	private Button _refreshBalancesButton = null!;
	private Button _createWalletButton = null!;

	public override void _Ready()
	{
		_statusLabel = GetNode<Label>("%StatusLabel");
		_addressValueLabel = GetNode<Label>("%AddressValueLabel");
		_balancesList = GetNode<ItemList>("%BalancesList");
		_logOutput = GetNode<RichTextLabel>("%LogOutput");
		_createWalletButton = GetNode<Button>("%CreateWalletButton");
		_importWalletButton = GetNode<Button>("%ImportWalletButton");
		_unlockButton = GetNode<Button>("%UnlockButton");
		_lockButton = GetNode<Button>("%LockButton");
		_copyAddressButton = GetNode<Button>("%CopyAddressButton");
		_refreshBalancesButton = GetNode<Button>("%RefreshBalancesButton");

		_createWalletButton.Pressed += OnCreateWalletPressed;
		_importWalletButton.Pressed += OnImportWalletPressed;
		_unlockButton.Pressed += OnUnlockPressed;
		_lockButton.Pressed += OnLockPressed;
		_copyAddressButton.Pressed += OnCopyAddressPressed;
		_refreshBalancesButton.Pressed += OnRefreshBalancesPressed;

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
		bool hasWallet = _walletService.HasWallet();
		bool isUnlocked = _walletService.IsUnlocked();
		
		_statusLabel.Text = BuildStatusText();
		_addressValueLabel.Text = hasWallet
			? _walletService.GetAddress()
			: "No wallet loaded";

		_createWalletButton.Disabled = hasWallet;
		_importWalletButton.Disabled = hasWallet;
		_unlockButton.Disabled = !hasWallet || isUnlocked;
		_lockButton.Disabled = !hasWallet || !isUnlocked;
		_copyAddressButton.Disabled = !hasWallet || !isUnlocked;
		_refreshBalancesButton.Disabled = !hasWallet || !isUnlocked;

		RefreshBalances();
	}

	private string BuildStatusText()
	{
		if (!_walletService.HasWallet())
			return "Status: No wallet";

		return _walletService.IsUnlocked()
			? "Status: Wallet unlocked"
			: "Status: Wallet locked";
	}
	
private void RefreshBalances()
{
	_balancesList.Clear();

	if (!_walletService.HasWallet())
	{
		_balancesList.AddItem("No wallet loaded");
		return;
	}

	if (!_walletService.IsUnlocked())
	{
		_balancesList.AddItem("Wallet locked");
		return;
	}

	var balances = _walletService.GetBalances();

	if (balances.Count == 0)
	{
		_balancesList.AddItem("No balances");
		return;
	}

	foreach (var balance in balances)
	{
		_balancesList.AddItem($"{balance.Symbol}: {balance.DisplayAmount}");
	}
}

	private void Log(string message)
	{
		_logOutput.AppendText($"{message}\n");
	}
}
