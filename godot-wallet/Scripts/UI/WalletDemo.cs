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
	private AcceptDialog _simpleMessageDialog = null!;
	private ConfirmationDialog _importPrivateKeyDialog = null!;
	private LineEdit _importPrivateKeyInput = null!;
	private ConfirmationDialog _passwordDialog = null!;
	private Label _passwordDialogLabel = null!;
	private LineEdit _passwordInput = null!;
	private Button _importMnemonicButton = null!;
	private ConfirmationDialog _importMnemonicDialog = null!;
	private LineEdit _importMnemonicInput = null!;

	private PendingPasswordAction _pendingPasswordAction = PendingPasswordAction.None;
	private string _pendingImportPrivateKey = "";
	private string _pendingMnemonic = "";

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
		
		_walletService.LoadWalletMetadataIfPresent();

		RefreshUi();
		Log("Wallet demo ready.");
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
		_walletService.Lock();
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

	private void OnRefreshBalancesPressed()
	{
		Log("Refreshing balances.");
		RefreshUi();
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
	
	private void OnPasswordDialogConfirmed()
	{
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
					_walletService.CreateWallet(password);
					RefreshUi();

					var phrase = _walletService.ConsumePendingRecoveryPhrase();
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
					_walletService.ImportPrivateKey(_pendingImportPrivateKey, password);
					_pendingImportPrivateKey = "";
					RefreshUi();
					Log("Imported wallet and saved encrypted wallet file.");
					break;

				case PendingPasswordAction.UnlockWallet:
					bool ok = _walletService.Unlock(password);
					RefreshUi();
					Log(ok ? "Wallet unlocked." : "Unlock failed.");
					break;

				case PendingPasswordAction.ImportMnemonic:
					_walletService.ImportMnemonic(_pendingMnemonic, password);
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

	private void RefreshUi()
	{
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
		_importMnemonicButton.Disabled = hasWallet;

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
