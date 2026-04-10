using Godot;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet
{
	/// <summary>
	/// Game-initiated message signing request. Shows a confirmation dialog with the
	/// full message, then signs it with the player's key using EIP-191 format.
	/// If the wallet is locked, prompts unlock and resumes after.
	/// </summary>
	public void RequestSignMessage(string message)
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet())
		{
			Log("Create or import a wallet before signing.");
			MessageSignDeclined?.Invoke();
			return;
		}

		if (string.IsNullOrEmpty(message))
		{
			Log("Cannot sign an empty message.");
			MessageSignDeclined?.Invoke();
			return;
		}

		_pendingSignMessage = message;

		if (!walletService.IsUnlocked())
		{
			Log("Unlock the wallet to sign the message.");
			OpenPasswordDialog(
				PendingPasswordAction.UnlockWallet,
				"Enter your wallet password to unlock."
			);
			return;
		}

		OpenSignMessageDialog(message);
	}

	private void OpenSignMessageDialog(string message)
	{
		ResetIdleTimer();
		_signMessageContent.Text = message;
		_signMessageDialog.PopupCentered(new Vector2I(520, 320));
	}

	private void ConsumePendingSignMessage()
	{
		if (string.IsNullOrEmpty(_pendingSignMessage))
		{
			return;
		}

		OpenSignMessageDialog(_pendingSignMessage);
	}

	private void OnSignMessageConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}

		ResetIdleTimer();
		var walletService = _walletService!;

		string message = _pendingSignMessage;
		_pendingSignMessage = "";

		if (!walletService.IsUnlocked())
		{
			Log("Cannot sign: wallet is locked.");
			MessageSignDeclined?.Invoke();
			return;
		}

		try
		{
			string signature = walletService.SignMessage(message);
			string address = FormatAsGalaAddress(walletService.GetAddress());

			Log($"Signed message for {address}");
			MessageSigned?.Invoke(message, signature, address);
		}
		catch (System.Exception ex)
		{
			Log($"Sign message failed: {ex.Message}");
			MessageSignDeclined?.Invoke();
		}
	}

	private void OnSignMessageCanceled()
	{
		_pendingSignMessage = "";
		Log("Message signing declined.");
		MessageSignDeclined?.Invoke();
	}
}
