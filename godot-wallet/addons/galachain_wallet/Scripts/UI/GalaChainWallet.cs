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

	private Button _burnButton = null!;
	private ConfirmationDialog _burnDialog = null!;
	private Label _burnSelectedTokenLabel = null!;
	private Label _burnQuantityLabel = null!;
	private LineEdit _burnQuantityInput = null!;
	private Label _burnSummaryLabel = null!;
	private TokenBalanceModel? _selectedBurnToken;

	private Button _grantAllowanceButton = null!;
	private ConfirmationDialog _grantAllowanceDialog = null!;
	private Label _grantAllowanceSelectedTokenLabel = null!;
	private OptionButton _grantAllowanceTypeOption = null!;
	private Label _grantAllowanceSpenderLabel = null!;
	private LineEdit _grantAllowanceSpenderInput = null!;
	private Label _grantAllowanceQuantityLabel = null!;
	private LineEdit _grantAllowanceQuantityInput = null!;
	private Label _grantAllowanceExpiresLabel = null!;
	private LineEdit _grantAllowanceExpiresInput = null!;
	private Label _grantAllowanceSummaryLabel = null!;
	private TokenBalanceModel? _selectedGrantAllowanceToken;

	private ConfirmationDialog _signMessageDialog = null!;
	private RichTextLabel _signMessageContent = null!;
	private string _pendingSignMessage = "";

	private PendingPasswordAction _pendingPasswordAction = PendingPasswordAction.None;
	private string _pendingImportPrivateKey = "";
	private string _pendingMnemonic = "";

	private string? _pendingTransferTo;
	private string? _pendingTransferQuantity;
	private string? _pendingTransferSymbol;

	private string? _pendingBurnQuantity;
	private string? _pendingBurnSymbol;

	private string? _pendingGrantAllowanceSpender;
	private string? _pendingGrantAllowanceQuantity;
	private string? _pendingGrantAllowanceSymbol;
	private AllowanceType _pendingGrantAllowanceType = AllowanceType.Transfer;
	private long _pendingGrantAllowanceExpiresUnixMs;

	private const double IdleTimeoutSeconds = 300.0; // 5 minutes
	private double _lastActivityTime;

	private static readonly System.Net.Http.HttpClient IconHttp = new();
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> IconCache = new();

	// Events — WalletFacade subscribes to these and re-exposes for game code
	public event Action<string>? WalletCreated;
	public event Action<string>? WalletImported;
	public event Action<string>? WalletUnlocked;
	public event Action? WalletLocked;
	public event Action<string, string, string>? TransferCompleted;
	public event Action<string>? TransferFailed;
	public event Action<string, string>? BurnCompleted;
	public event Action<string>? BurnFailed;
	public event Action<string, string, string, string>? AllowanceGranted; // spender, qty, symbol, allowanceType ("Transfer"/"Burn")
	public event Action<string>? AllowanceGrantFailed;
	public event Action<string, string, string>? MessageSigned;
	public event Action? MessageSignDeclined;
	public event Action? BalancesRefreshed;

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
		_burnButton = GetNode<Button>("%BurnButton");
		_burnDialog = GetNode<ConfirmationDialog>("%BurnDialog");
		_burnSelectedTokenLabel = GetNode<Label>("%BurnSelectedTokenLabel");
		_burnQuantityLabel = GetNode<Label>("%BurnQuantityLabel");
		_burnQuantityInput = GetNode<LineEdit>("%BurnQuantityInput");
		_burnSummaryLabel = GetNode<Label>("%BurnSummaryLabel");
		_grantAllowanceButton = GetNode<Button>("%GrantAllowanceButton");
		_grantAllowanceDialog = GetNode<ConfirmationDialog>("%GrantAllowanceDialog");
		_grantAllowanceSelectedTokenLabel = GetNode<Label>("%GrantAllowanceSelectedTokenLabel");
		_grantAllowanceTypeOption = GetNode<OptionButton>("%GrantAllowanceTypeOption");
		_grantAllowanceSpenderLabel = GetNode<Label>("%GrantAllowanceSpenderLabel");
		_grantAllowanceSpenderInput = GetNode<LineEdit>("%GrantAllowanceSpenderInput");
		_grantAllowanceQuantityLabel = GetNode<Label>("%GrantAllowanceQuantityLabel");
		_grantAllowanceQuantityInput = GetNode<LineEdit>("%GrantAllowanceQuantityInput");
		_grantAllowanceExpiresLabel = GetNode<Label>("%GrantAllowanceExpiresLabel");
		_grantAllowanceExpiresInput = GetNode<LineEdit>("%GrantAllowanceExpiresInput");
		_grantAllowanceSummaryLabel = GetNode<Label>("%GrantAllowanceSummaryLabel");
		_signMessageDialog = GetNode<ConfirmationDialog>("%SignMessageDialog");
		_signMessageContent = GetNode<RichTextLabel>("%SignMessageContent");

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
		_burnButton.Pressed += OnBurnPressed;
		_burnDialog.Confirmed += OnBurnDialogConfirmed;
		_burnQuantityInput.TextChanged += OnBurnInputChanged;
		_grantAllowanceButton.Pressed += OnGrantAllowancePressed;
		_grantAllowanceDialog.Confirmed += OnGrantAllowanceDialogConfirmed;
		_grantAllowanceSpenderInput.TextChanged += OnGrantAllowanceInputChanged;
		_grantAllowanceQuantityInput.TextChanged += OnGrantAllowanceInputChanged;
		_grantAllowanceExpiresInput.TextChanged += OnGrantAllowanceInputChanged;
		_grantAllowanceTypeOption.ItemSelected += _ => UpdateGrantAllowanceSummary();
		_signMessageDialog.Confirmed += OnSignMessageConfirmed;
		_signMessageDialog.Canceled += OnSignMessageCanceled;

		_copyPhraseButton.Pressed += OnCopyPhrasePressed;

		_passwordInput.TextSubmitted += _ => { _passwordDialog.Hide(); OnPasswordDialogConfirmed(); };
		_importPrivateKeyInput.TextSubmitted += _ => { _importPrivateKeyDialog.Hide(); OnImportPrivateKeyConfirmed(); };
		_importMnemonicInput.TextSubmitted += _ => { _importMnemonicDialog.Hide(); OnImportMnemonicConfirmed(); };
		_transferQuantityInput.TextSubmitted += _ => { _transferDialog.Hide(); OnTransferDialogConfirmed(); };
		_burnQuantityInput.TextSubmitted += _ => { _burnDialog.Hide(); OnBurnDialogConfirmed(); };
		_grantAllowanceExpiresInput.TextSubmitted += _ => { _grantAllowanceDialog.Hide(); OnGrantAllowanceDialogConfirmed(); };

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
			WalletLocked?.Invoke();
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

		if (_burnButton != null)
		{
			_burnButton.Disabled = true;
		}

		if (_grantAllowanceButton != null)
		{
			_grantAllowanceButton.Disabled = true;
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

		if (_burnButton != null)
		{
			_burnButton.Disabled = !hasWallet || !isUnlocked;
		}

		if (_grantAllowanceButton != null)
		{
			_grantAllowanceButton.Disabled = !hasWallet || !isUnlocked;
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

		for (int i = 0; i < balances.Count; i++)
		{
			var balance = balances[i];
			_balancesList.AddItem($"{balance.Symbol}: {balance.DisplayAmount}");

			if (!string.IsNullOrWhiteSpace(balance.ImageUrl))
			{
				LoadIconAsync(balance.ImageUrl, i);
			}
		}
	}

	private async void LoadIconAsync(string url, int itemIndex)
	{
		if (IconCache.TryGetValue(url, out var cached))
		{
			if (itemIndex >= 0 && itemIndex < _balancesList.ItemCount)
				_balancesList.SetItemIcon(itemIndex, cached);
			return;
		}

		// Retry up to 3 times — .NET HttpClient has intermittent TLS failures
		// with some CDN domains in the Godot runtime
		byte[]? data = null;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			try
			{
				data = await IconHttp.GetByteArrayAsync(url);
				break;
			}
			catch
			{
				if (attempt < 2)
					await System.Threading.Tasks.Task.Delay(500);
			}
		}

		if (data == null || data.Length < 4)
			return;

		var image = new Image();
		bool loaded = false;

		if (data[0] == 0x89 && data[1] == 0x50)
			loaded = image.LoadPngFromBuffer(data) == Error.Ok;
		else if (data[0] == 0xFF && data[1] == 0xD8)
			loaded = image.LoadJpgFromBuffer(data) == Error.Ok;
		else if (data[0] == 0x52 && data[1] == 0x49)
			loaded = image.LoadWebpFromBuffer(data) == Error.Ok;

		if (!loaded)
			return;

		image.Resize(24, 24);
		var texture = ImageTexture.CreateFromImage(image);
		IconCache[url] = texture;

		if (itemIndex >= 0 && itemIndex < _balancesList.ItemCount)
			_balancesList.SetItemIcon(itemIndex, texture);
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
