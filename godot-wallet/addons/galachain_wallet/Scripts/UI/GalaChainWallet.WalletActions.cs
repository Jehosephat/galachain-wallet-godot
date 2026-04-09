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
	}

	private void OnCopyAddressPressed()
	{
		if (!EnsureService())
		{
			return;
		}

		string address = _walletService!.GetAddress();
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

		ResetIdleTimer();

		Log("Refreshing balances from GalaChain...");
		var result = await walletService.RefreshBalancesAsync();
		RefreshUi();

		if (result.IsSuccess)
			Log("Balances refreshed.");
		else
			Log($"Balance refresh failed: {result.ErrorMessage}");
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
