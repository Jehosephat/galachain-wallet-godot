using Godot;
using System;
using System.Collections.Generic;
using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet : Control
{
	private IWalletService? _walletService;
	private bool _uiReady = false;

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
	private AcceptDialog _simpleMessageDialog = null!;
	private AcceptDialog _recoveryPhraseDialog = null!;
	private GridContainer _recoveryPhraseGrid = null!;
	private Button _copyPhraseButton = null!;
	private string _currentRecoveryPhrase = "";
	private ConfirmationDialog _importPrivateKeyDialog = null!;
	private LineEdit _importPrivateKeyInput = null!;
	private ConfirmationDialog _passwordDialog = null!;
	private Label _passwordDialogLabel = null!;
	private LineEdit _passwordInput = null!;
	private Button _importMnemonicButton = null!;
	private ConfirmationDialog _importMnemonicDialog = null!;
	private LineEdit _importMnemonicInput = null!;
	private Button _transferButton = null!;
	private ConfirmationDialog _transferDialog = null!;
	private Label _transferSelectedTokenLabel = null!;
	private Label _transferToLabel = null!;
	private LineEdit _transferToInput = null!;
	private Label _transferQuantityLabel = null!;
	private LineEdit _transferQuantityInput = null!;
	private Label _transferSummaryLabel = null!;
	private TokenBalanceModel? _selectedTransferToken;

	private PendingPasswordAction _pendingPasswordAction = PendingPasswordAction.None;
	private string _pendingImportPrivateKey = "";
	private string _pendingMnemonic = "";

	private string? _pendingTransferTo;
	private string? _pendingTransferQuantity;
	private string? _pendingTransferSymbol;

	private const double IdleTimeoutSeconds = 300.0; // 5 minutes
	private double _lastActivityTime;

	public void Initialize(IWalletService walletService)
	{
		_walletService = walletService;

		if (_uiReady)
		{
			RefreshUi();
		}
	}

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
		_simpleMessageDialog = GetNode<AcceptDialog>("%SimpleMessageDialog");
		_recoveryPhraseDialog = GetNode<AcceptDialog>("%RecoveryPhraseDialog");
		_recoveryPhraseGrid = GetNode<GridContainer>("%RecoveryPhraseGrid");
		_copyPhraseButton = GetNode<Button>("%CopyPhraseButton");
		_importPrivateKeyDialog = GetNode<ConfirmationDialog>("%ImportPrivateKeyDialog");
		_importPrivateKeyInput = GetNode<LineEdit>("%ImportPrivateKeyInput");
		_passwordDialog = GetNode<ConfirmationDialog>("%PasswordDialog");
		_passwordDialogLabel = GetNode<Label>("%PasswordDialogLabel");
		_passwordInput = GetNode<LineEdit>("%PasswordInput");
		_importMnemonicButton = GetNode<Button>("%ImportMnemonicButton");
		_importMnemonicDialog = GetNode<ConfirmationDialog>("%ImportMnemonicDialog");
		_importMnemonicInput = GetNode<LineEdit>("%ImportMnemonicInput");
		_transferButton = GetNode<Button>("%TransferButton");
		_transferDialog = GetNode<ConfirmationDialog>("%TransferDialog");
		_transferSelectedTokenLabel = GetNode<Label>("%TransferSelectedTokenLabel");
		_transferToLabel = GetNode<Label>("%TransferToLabel");
		_transferToInput = GetNode<LineEdit>("%TransferToInput");
		_transferQuantityLabel = GetNode<Label>("%TransferQuantityLabel");
		_transferQuantityInput = GetNode<LineEdit>("%TransferQuantityInput");
		_transferSummaryLabel = GetNode<Label>("%TransferSummaryLabel");

		_createWalletButton.Pressed += OnCreateWalletPressed;
		_importWalletButton.Pressed += OnImportWalletPressed;
		_unlockButton.Pressed += OnUnlockPressed;
		_lockButton.Pressed += OnLockPressed;
		_copyAddressButton.Pressed += OnCopyAddressPressed;
		_refreshBalancesButton.Pressed += OnRefreshBalancesPressed;
		_importPrivateKeyDialog.Confirmed += OnImportPrivateKeyConfirmed;
		_passwordDialog.Confirmed += OnPasswordDialogConfirmed;
		_importMnemonicButton.Pressed += OnImportMnemonicPressed;
		_importMnemonicDialog.Confirmed += OnImportMnemonicConfirmed;
		_transferButton.Pressed += OnTransferPressed;
		_transferDialog.Confirmed += OnTransferDialogConfirmed;
		_transferToInput.TextChanged += OnTransferInputChanged;
		_transferQuantityInput.TextChanged += OnTransferInputChanged;

		_copyPhraseButton.Pressed += OnCopyPhrasePressed;

		_passwordInput.TextSubmitted += _ => { _passwordDialog.Hide(); OnPasswordDialogConfirmed(); };
		_importPrivateKeyInput.TextSubmitted += _ => { _importPrivateKeyDialog.Hide(); OnImportPrivateKeyConfirmed(); };
		_importMnemonicInput.TextSubmitted += _ => { _importMnemonicDialog.Hide(); OnImportMnemonicConfirmed(); };
		_transferQuantityInput.TextSubmitted += _ => { _transferDialog.Hide(); OnTransferDialogConfirmed(); };

		_uiReady = true;
		_lastActivityTime = Time.GetTicksMsec() / 1000.0;

		if (_walletService != null)
		{
			_walletService.LoadWalletMetadataIfPresent();
			RefreshUi();
			Log("Wallet ready.");
		}
		else
		{
			ShowUninitializedState();
		}
	}

	public override void _Process(double delta)
	{
		if (_walletService == null || !_walletService.IsUnlocked())
			return;

		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastActivityTime >= IdleTimeoutSeconds)
		{
			_walletService.Lock();
			RefreshUi();
			Log("Wallet auto-locked after inactivity.");
		}
	}

	private void ResetIdleTimer()
	{
		_lastActivityTime = Time.GetTicksMsec() / 1000.0;
	}

	private void ShowUninitializedState()
	{
		_statusLabel.Text = "Status: Wallet not initialized";
		_addressValueLabel.Text = "No wallet service";
		_balancesList.Clear();
		_balancesList.AddItem("Wallet unavailable");

		_createWalletButton.Disabled = true;
		_importWalletButton.Disabled = true;
		_unlockButton.Disabled = true;
		_lockButton.Disabled = true;
		_copyAddressButton.Disabled = true;
		_refreshBalancesButton.Disabled = true;

		if (_transferButton != null)
		{
			_transferButton.Disabled = true;
		}
	}

	private bool EnsureService()
	{
		if (_walletService != null)
		{
			return true;
		}

		Log("Wallet service is not initialized.");
		return false;
	}

	private void RefreshUi()
	{
		if (!_uiReady)
		{
			return;
		}

		if (_walletService == null)
		{
			ShowUninitializedState();
			return;
		}

		bool hasWallet = _walletService.HasWallet();
		bool isUnlocked = _walletService.IsUnlocked();

		if (hasWallet)
		{
			_statusLabel.Text = isUnlocked
				? "Status: Wallet unlocked"
				: "Status: Wallet locked";

			_addressValueLabel.Text = FormatAsGalaAddress(_walletService.GetAddress());
		}
		else
		{
			_statusLabel.Text = "Status: No wallet";
			_addressValueLabel.Text = "No wallet loaded";
		}

		_createWalletButton.Disabled = hasWallet;
		_importWalletButton.Disabled = hasWallet;
		_unlockButton.Disabled = !hasWallet || isUnlocked;
		_lockButton.Disabled = !hasWallet || !isUnlocked;
		_copyAddressButton.Disabled = !hasWallet || !isUnlocked;
		_refreshBalancesButton.Disabled = !hasWallet || !isUnlocked;

		if (_transferButton != null)
		{
			_transferButton.Disabled = !hasWallet || !isUnlocked;
		}

		RefreshBalances();
	}

	private void RefreshBalances()
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		_balancesList.Clear();

		if (!walletService.HasWallet())
		{
			_balancesList.AddItem("No wallet loaded");
			return;
		}

		if (!walletService.IsUnlocked())
		{
			_balancesList.AddItem("Wallet locked");
			return;
		}

		var balances = walletService.GetBalances();

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

	private static string FormatAsGalaAddress(string address)
	{
		if (string.IsNullOrWhiteSpace(address))
			return address;

		if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			return $"eth|{address[2..]}";

		return address;
	}
}
