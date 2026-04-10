using Godot;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet
{
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
		WalletLocked?.Invoke();
	}

	private void OnCopyAddressPressed()
	{
		if (!EnsureService())
		{
			return;
		}

		string address = FormatAsGalaAddress(_walletService!.GetAddress());
		if (string.IsNullOrWhiteSpace(address))
		{
			Log("No address to copy.");
			return;
		}

		DisplayServer.ClipboardSet(address);
		ResetIdleTimer();
		Log($"Copied address: {address}");
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

		ResetIdleTimer();

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
						ShowRecoveryPhraseDialog(phrase);
					}

					Log("Created wallet and saved encrypted wallet file.");
					WalletCreated?.Invoke(walletService.GetAddress());
					BalancesRefreshed?.Invoke();
					break;

				case PendingPasswordAction.ImportWallet:
					walletService.ImportPrivateKey(_pendingImportPrivateKey, password);
					_pendingImportPrivateKey = "";
					await walletService.RefreshBalancesAsync();
					RefreshUi();
					Log("Imported wallet and saved encrypted wallet file.");
					WalletImported?.Invoke(walletService.GetAddress());
					BalancesRefreshed?.Invoke();
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
						WalletUnlocked?.Invoke(walletService.GetAddress());
						BalancesRefreshed?.Invoke();
						ConsumePendingTransfer();
						ConsumePendingBurn();
						ConsumePendingSignMessage();
					}
					break;

				case PendingPasswordAction.ImportMnemonic:
					walletService.ImportMnemonic(_pendingMnemonic, password);
					_pendingMnemonic = "";
					await walletService.RefreshBalancesAsync();
					RefreshUi();
					Log("Imported wallet from recovery phrase and saved encrypted wallet file.");
					WalletImported?.Invoke(walletService.GetAddress());
					BalancesRefreshed?.Invoke();
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

		ResetIdleTimer();

		Log("Refreshing balances from GalaChain...");
		var result = await walletService.RefreshBalancesAsync();
		RefreshUi();

		if (result.IsSuccess)
		{
			Log("Balances refreshed.");
			BalancesRefreshed?.Invoke();
		}
		else
		{
			Log($"Balance refresh failed: {result.ErrorMessage}");
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

	private void ShowRecoveryPhraseDialog(string phrase)
	{
		_currentRecoveryPhrase = phrase;

		// Clear previous grid content
		foreach (var child in _recoveryPhraseGrid.GetChildren())
		{
			child.QueueFree();
		}

		// Populate grid with numbered words
		string[] words = phrase.Split(' ');
		for (int i = 0; i < words.Length; i++)
		{
			var label = new Label();
			label.Text = $"{i + 1}. {words[i]}";
			_recoveryPhraseGrid.AddChild(label);
		}

		_recoveryPhraseDialog.PopupCentered(new Vector2I(480, 340));
	}

	private void OnCopyPhrasePressed()
	{
		if (!string.IsNullOrWhiteSpace(_currentRecoveryPhrase))
		{
			DisplayServer.ClipboardSet(_currentRecoveryPhrase);
			Log("Recovery phrase copied to clipboard.");
		}
	}
}
