using Godot;
using System;
using System.Text;
using System.Collections.Generic;

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
	private LineEdit _transferToInput = null!;
	private LineEdit _transferQuantityInput = null!;
	private Label _transferSummaryLabel = null!;

	private TokenBalanceModel? _selectedTransferToken;

	private PendingPasswordAction _pendingPasswordAction = PendingPasswordAction.None;
	private string _pendingImportPrivateKey = "";
	private string _pendingMnemonic = "";

	private string? _pendingTransferTo;
	private string? _pendingTransferQuantity;
	private string? _pendingTransferSymbol;

	public void Initialize(IWalletService walletService)
	{
		_walletService = walletService;
		
		if(_uiReady)
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
		_importPrivateKeyDialog = GetNode<ConfirmationDialog>("%ImportPrivateKeyDialog");
		_importPrivateKeyInput = GetNode<LineEdit>("%ImportPrivateKeyInput");_passwordDialog = GetNode<ConfirmationDialog>("%PasswordDialog");
		_passwordDialogLabel = GetNode<Label>("%PasswordDialogLabel");
		_passwordInput = GetNode<LineEdit>("%PasswordInput");
		_importMnemonicButton = GetNode<Button>("%ImportMnemonicButton");
		_importMnemonicDialog = GetNode<ConfirmationDialog>("%ImportMnemonicDialog");
		_importMnemonicInput = GetNode<LineEdit>("%ImportMnemonicInput");
		_transferButton = GetNode<Button>("%TransferButton");
		_transferDialog = GetNode<ConfirmationDialog>("%TransferDialog");
		_transferSelectedTokenLabel = GetNode<Label>("%TransferSelectedTokenLabel");
		_transferToInput = GetNode<LineEdit>("%TransferToInput");
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
		
		_walletService.LoadWalletMetadataIfPresent();
		_uiReady = true;

		if (_walletService != null)
		{
			RefreshUi();
			Log("Wallet ready.");
		}
		else
		{
			ShowUninitializedState();
		}
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

	private void OnCreateWalletPressed()
	{	
		OpenPasswordDialog(
			PendingPasswordAction.CreateWallet,
	        "Choose a password to encrypt this wallet."
		);
	}
	
	private void OnImportWalletPressed()
	{		
		_importPrivateKeyInput.Text = "";
		_importPrivateKeyDialog.PopupCentered(new Vector2I(500, 180));
	}

	private void OnUnlockPressed()
	{
		OpenPasswordDialog(
			PendingPasswordAction.UnlockWallet,
	        "Enter your wallet password to unlock."
		);
	}

	private void OnLockPressed()
	{
		if (!EnsureService())
		{
			return;
		}
		
		var walletService = _walletService!;
		
		walletService.Lock();
		RefreshUi();
		Log("Wallet locked.");
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
	
	private void OnTransferPressed()
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet() || !_walletService.IsUnlocked())
		{
			Log("Unlock the wallet before transferring.");
			return;
		}

		var selected = _balancesList.GetSelectedItems();
		if (selected.Length == 0)
		{
			Log("Select a token balance first.");
			return;
		}

		int selectedIndex = selected[0];
		var balances = walletService.GetBalances();

		if (selectedIndex < 0 || selectedIndex >= balances.Count)
		{
			Log("Selected balance index was invalid.");
			return;
		}

		OpenTransferDialog(balances[selectedIndex], "", "");
	}

	public void RequestTransfer(string toAddress, string quantity, string tokenSymbol)
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet())
		{
			Log("Create or import a wallet before transferring.");
			return;
		}

		if (!walletService.IsUnlocked())
		{
			_pendingTransferTo = toAddress;
			_pendingTransferQuantity = quantity;
			_pendingTransferSymbol = tokenSymbol;
			Log("Unlock the wallet to complete the transfer.");
			OpenPasswordDialog(
				PendingPasswordAction.UnlockWallet,
				"Enter your wallet password to unlock."
			);
			return;
		}

		ExecuteTransferRequest(toAddress, quantity, tokenSymbol);
	}

	private void ExecuteTransferRequest(string toAddress, string quantity, string tokenSymbol)
	{
		var balances = _walletService!.GetBalances();
		TokenBalanceModel? match = null;

		foreach (var b in balances)
		{
			if (string.Equals(b.Symbol, tokenSymbol, StringComparison.OrdinalIgnoreCase))
			{
				match = b;
				break;
			}
		}

		if (match == null)
		{
			Log($"No balance found for token \"{tokenSymbol}\". Refresh balances and try again.");
			return;
		}

		OpenTransferDialog(match, toAddress, quantity);
	}

	private void ConsumePendingTransfer()
	{
		if (_pendingTransferTo == null)
		{
			return;
		}

		string to = _pendingTransferTo;
		string quantity = _pendingTransferQuantity ?? "";
		string symbol = _pendingTransferSymbol ?? "";

		_pendingTransferTo = null;
		_pendingTransferQuantity = null;
		_pendingTransferSymbol = null;

		ExecuteTransferRequest(to, quantity, symbol);
	}

	private void OpenTransferDialog(TokenBalanceModel token, string toAddress, string quantity)
	{
		_selectedTransferToken = token;

		_transferSelectedTokenLabel.Text =
			$"Token: {_selectedTransferToken.Symbol} | Available: {_selectedTransferToken.AvailableAmount:0.########}";

		_transferToInput.Text = toAddress;
		_transferQuantityInput.Text = quantity;
		UpdateTransferSummary();

		_transferDialog.PopupCentered(new Vector2I(520, 240));
		_transferToInput.GrabFocus();
	}
	
	private void OnImportPrivateKeyConfirmed()
	{
		var privateKey = _importPrivateKeyInput.Text.Trim();

		if (string.IsNullOrWhiteSpace(privateKey))
		{
			Log("Import failed: private key was empty.");
			return;
		}

		_pendingImportPrivateKey = privateKey;

		OpenPasswordDialog(
			PendingPasswordAction.ImportWallet,
	        "Enter a password to encrypt the imported wallet."
		);
	}
	
	private async void OnPasswordDialogConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}
		
		var walletService = _walletService!;
		
		string password = _passwordInput.Text;

		if (string.IsNullOrWhiteSpace(password))
		{
			Log("Password entry was empty.");
			return;
		}

		try
		{
			switch (_pendingPasswordAction)
			{
				case PendingPasswordAction.CreateWallet:
					walletService.CreateWallet(password);
					await walletService.RefreshBalancesAsync();
					RefreshUi();

					var phrase = walletService.ConsumePendingRecoveryPhrase();
					if (!string.IsNullOrWhiteSpace(phrase))
					{
						_simpleMessageDialog.Title = "Recovery Phrase";
						_simpleMessageDialog.DialogText =
							"Write this down and store it safely.\n\n" +
							"This is the only time it will be shown:\n\n" +
							phrase;

						_simpleMessageDialog.PopupCentered();
					}

					Log("Created wallet and saved encrypted wallet file.");
					break;

				case PendingPasswordAction.ImportWallet:
					walletService.ImportPrivateKey(_pendingImportPrivateKey, password);
					_pendingImportPrivateKey = "";
					await walletService.RefreshBalancesAsync();
					RefreshUi();
					Log("Imported wallet and saved encrypted wallet file.");
					break;

				case PendingPasswordAction.UnlockWallet:
					bool ok = walletService.Unlock(password);
					if (ok)
					{
						await walletService.RefreshBalancesAsync();
					}
					RefreshUi();
					Log(ok ? "Wallet unlocked." : "Unlock failed.");
					if (ok)
					{
						ConsumePendingTransfer();
					}
					break;

				case PendingPasswordAction.ImportMnemonic:
					walletService.ImportMnemonic(_pendingMnemonic, password);
					_pendingMnemonic = "";
					RefreshUi();
					Log("Imported wallet from recovery phrase and saved encrypted wallet file.");
					break;

				default:
					Log("No password action was pending.");
					break;
			}
		}
		catch (System.Exception ex)
		{
			Log($"Password action failed: {ex.Message}");
		}
		finally
		{
			_pendingPasswordAction = PendingPasswordAction.None;
			_passwordInput.Text = "";
		}
	}
	
	private void OnImportMnemonicPressed()
	{
		_importMnemonicInput.Text = "";
		_importMnemonicDialog.PopupCentered(new Vector2I(620, 180));
		_importMnemonicInput.GrabFocus();
	}

	private void OnImportMnemonicConfirmed()
	{
		string mnemonic = _importMnemonicInput.Text.Trim();

		if (string.IsNullOrWhiteSpace(mnemonic))
		{
			Log("Mnemonic import failed: phrase was empty.");
			return;
		}

		_pendingMnemonic = mnemonic;

		OpenPasswordDialog(
			PendingPasswordAction.ImportMnemonic,
	        "Enter a password to encrypt the restored wallet."
		);
	}
	
	private async void OnRefreshBalancesPressed()
	{
		if (!EnsureService())
		{
			return;
		}
		
		var walletService = _walletService!;
		
		try
		{
			Log("Refreshing balances from GalaChain...");
			await walletService.RefreshBalancesAsync();
			RefreshUi();
			Log("Balances refreshed.");
		}
		catch (System.Exception ex)
		{
			Log($"Balance refresh failed: {ex.Message}");
		}
	}
	
	private void OnTransferInputChanged(string _newText)
	{
		UpdateTransferSummary();
	}

	private void UpdateTransferSummary()
	{
		if (!TryBuildTransferDraft(out var draft, out var error))
		{
			_transferSummaryLabel.Text = string.IsNullOrWhiteSpace(error)
				? ""
				: $"Preview unavailable: {error}";
			return;
		}

		_transferSummaryLabel.Text =
			$"You are about to transfer {draft.Quantity} {draft.DisplaySymbol}\n" +
			$"To: {draft.ToAddress}";
	}
	
	private async void OnTransferDialogConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}
		
		var walletService = _walletService!;
		
		if (!TryBuildTransferDraft(out var draft, out var error))
		{
			Log($"Transfer failed: {error}");
			UpdateTransferSummary();
			return;
		}

		try
		{
			Log($"Submitting transfer of {draft.Quantity} {draft.DisplaySymbol}...");
			await walletService.SubmitTransferAsync(draft);
			RefreshUi();
			Log("Transfer successful.");
		}
		catch (System.Exception ex)
		{
			Log($"Transfer failed: {ex.Message}");
		}
	}
	
	private bool TryBuildTransferDraft(out TransferDraft draft, out string error)
	{
		draft = new TransferDraft();
		error = "";

		if (_selectedTransferToken == null)
		{
			error = "No token selected.";
			return false;
		}

		string toAddress = _transferToInput.Text.Trim();
		string quantityText = _transferQuantityInput.Text.Trim();

		if (string.IsNullOrWhiteSpace(toAddress))
		{
			error = "Recipient address is required.";
			return false;
		}

		if (!toAddress.StartsWith("eth|", StringComparison.OrdinalIgnoreCase))
		{
			if(!toAddress.StartsWith("client|", StringComparison.OrdinalIgnoreCase))
			{
				error = "Recipient address must start with eht| or client|.";
				return false;
			}
		}

		if (string.Equals(toAddress, _walletService.GetAddress(), StringComparison.OrdinalIgnoreCase))
		{
			error = "Cannot transfer to the same wallet address.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(quantityText))
		{
			error = "Quantity is required.";
			return false;
		}

		if (!decimal.TryParse(
				quantityText,
				System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture,
				out var quantity))
		{
			error = "Quantity must be a valid number.";
			return false;
		}

		if (quantity <= 0m)
		{
			error = "Quantity must be greater than zero.";
			return false;
		}

		if (quantity > _selectedTransferToken.AvailableAmount)
		{
			error = "Quantity exceeds available balance.";
			return false;
		}

		draft = new TransferDraft
		{
			ToAddress = toAddress,
			Quantity = quantityText,
			DisplaySymbol = _selectedTransferToken.Symbol,
			TokenInstance = new GalaTokenInstance
			{
				collection = _selectedTransferToken.Collection,
				category = _selectedTransferToken.Category,
				type = _selectedTransferToken.Type,
				additionalKey = _selectedTransferToken.AdditionalKey,
				instance = _selectedTransferToken.Instance
			}
		};

		return true;
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

			_addressValueLabel.Text = _walletService.GetAddress();
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

	private string BuildStatusText()
	{
		if (!EnsureService())
		{
			return "";
		}
		
		var walletService = _walletService!;
		
		if (!walletService.HasWallet())
			return "Status: No wallet";

		return walletService.IsUnlocked()
			? "Status: Wallet unlocked"
			: "Status: Wallet locked";
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

	private void Log(string message){
		{
			_logOutput.AppendText($"{message}\n");
		}
	}

	private void OpenPasswordDialog(PendingPasswordAction action, string prompt)
	{
		_pendingPasswordAction = action;
		_passwordDialogLabel.Text = prompt;
		_passwordInput.Text = "";
		_passwordDialog.PopupCentered(new Vector2I(420, 160));
		_passwordInput.GrabFocus();
	}
}
