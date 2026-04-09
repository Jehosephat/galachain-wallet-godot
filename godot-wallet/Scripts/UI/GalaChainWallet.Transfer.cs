using Godot;
using System;
using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet
{
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
		ResetIdleTimer();
		_selectedTransferToken = token;

		_transferSelectedTokenLabel.Text =
			$"Token: {_selectedTransferToken.Symbol} | Available: {_selectedTransferToken.AvailableAmount:0.########}";

		_transferToInput.Text = toAddress;
		_transferQuantityInput.Text = quantity;
		UpdateTransferSummary();

		_transferDialog.PopupCentered(new Vector2I(520, 240));
		_transferToInput.GrabFocus();
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
			$"To: {draft.ToAddress}\n" +
			$"Estimated fee: loading...";

		RunDryRunPreview(draft);
	}

	private async void RunDryRunPreview(TransferDraft draft)
	{
		if (_walletService == null || !_walletService.IsUnlocked())
			return;

		var result = await _walletService.PreviewTransferAsync(draft);

		string feeDisplay;
		if (!result.IsSuccess)
		{
			feeDisplay = result.ErrorKind == NetworkErrorKind.TransportError
				? "unavailable (network error)"
				: $"unavailable ({result.ErrorMessage})";
		}
		else if (!result.Data.WouldSucceed)
		{
			feeDisplay = $"Preview failed: {result.Data.Message}";
		}
		else
		{
			feeDisplay = decimal.TryParse(result.Data.EstimatedFee,
				System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture,
				out var feeAmount) && feeAmount > 0m
				? $"{feeAmount:0.########} {result.Data.FeeToken}".Trim()
				: "None";
		}

		_transferSummaryLabel.Text =
			$"You are about to transfer {draft.Quantity} {draft.DisplaySymbol}\n" +
			$"To: {draft.ToAddress}\n" +
			$"Estimated fee: {feeDisplay}";
	}

	private async void OnTransferDialogConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}

		ResetIdleTimer();
		var walletService = _walletService!;

		if (!TryBuildTransferDraft(out var draft, out var error))
		{
			Log($"Transfer failed: {error}");
			UpdateTransferSummary();
			return;
		}

		Log($"Submitting transfer of {draft.Quantity} {draft.DisplaySymbol}...");
		var result = await walletService.SubmitTransferAsync(draft);
		RefreshUi();

		if (result.IsSuccess)
			Log("Transfer successful.");
		else
			Log($"Transfer failed: {result.ErrorMessage}");
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

		var result = _walletService!.ValidateTransfer(draft, _selectedTransferToken.AvailableAmount);
		if (!result.IsValid)
		{
			error = result.Error;
			return false;
		}

		return true;
	}
}
