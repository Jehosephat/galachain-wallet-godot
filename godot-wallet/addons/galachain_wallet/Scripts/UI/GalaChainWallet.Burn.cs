using Godot;
using System;
using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet
{
	private void OnBurnPressed()
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet() || !walletService.IsUnlocked())
		{
			Log("Unlock the wallet before burning.");
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

		OpenBurnDialog(balances[selectedIndex], "", readOnly: false);
	}

	public void RequestBurn(string quantity, string tokenSymbol)
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet())
		{
			Log("Create or import a wallet before burning.");
			return;
		}

		if (!walletService.IsUnlocked())
		{
			_pendingBurnQuantity = quantity;
			_pendingBurnSymbol = tokenSymbol;
			Log("Unlock the wallet to complete the burn.");
			OpenPasswordDialog(
				PendingPasswordAction.UnlockWallet,
				"Enter your wallet password to unlock."
			);
			return;
		}

		ExecuteBurnRequest(quantity, tokenSymbol);
	}

	private void ExecuteBurnRequest(string quantity, string tokenSymbol)
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

		OpenBurnDialog(match, quantity, readOnly: true);
	}

	private void ConsumePendingBurn()
	{
		if (_pendingBurnQuantity == null)
		{
			return;
		}

		string quantity = _pendingBurnQuantity;
		string symbol = _pendingBurnSymbol ?? "";

		_pendingBurnQuantity = null;
		_pendingBurnSymbol = null;

		ExecuteBurnRequest(quantity, symbol);
	}

	private void OpenBurnDialog(TokenBalanceModel token, string quantity, bool readOnly)
	{
		ResetIdleTimer();
		_selectedBurnToken = token;

		_burnSelectedTokenLabel.Text =
			$"Token: {_selectedBurnToken.Symbol} | Available: {_selectedBurnToken.AvailableAmount:0.########}";

		_burnQuantityInput.Text = quantity;

		_burnQuantityLabel.Visible = !readOnly;
		_burnQuantityInput.Visible = !readOnly;

		UpdateBurnSummary();

		_burnDialog.PopupCentered(new Vector2I(520, 220));
	}

	private void OnBurnInputChanged(string _newText)
	{
		UpdateBurnSummary();
	}

	private void UpdateBurnSummary()
	{
		if (!TryBuildBurnDraft(out var draft, out var error))
		{
			_burnSummaryLabel.Text = string.IsNullOrWhiteSpace(error)
				? ""
				: $"Preview unavailable: {error}";
			return;
		}

		_burnSummaryLabel.Text =
			$"You are about to burn {draft.Quantity} {draft.DisplaySymbol}\n" +
			$"Estimated fee: loading...\n" +
			$"This cannot be undone.";

		RunBurnDryRunPreview(draft);
	}

	private async void RunBurnDryRunPreview(BurnDraft draft)
	{
		if (_walletService == null || !_walletService.IsUnlocked())
			return;

		var result = await _walletService.PreviewBurnAsync(draft);

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

		_burnSummaryLabel.Text =
			$"You are about to burn {draft.Quantity} {draft.DisplaySymbol}\n" +
			$"Estimated fee: {feeDisplay}\n" +
			$"This cannot be undone.";
	}

	private async void OnBurnDialogConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}

		ResetIdleTimer();
		var walletService = _walletService!;

		if (!TryBuildBurnDraft(out var draft, out var error))
		{
			Log($"Burn failed: {error}");
			UpdateBurnSummary();
			return;
		}

		Log($"Submitting burn of {draft.Quantity} {draft.DisplaySymbol}...");
		var result = await walletService.SubmitBurnAsync(draft);
		RefreshUi();

		if (result.IsSuccess)
		{
			Log("Burn successful.");
			BurnCompleted?.Invoke(draft.Quantity, draft.DisplaySymbol);
			BalancesRefreshed?.Invoke();
		}
		else
		{
			Log($"Burn failed: {result.ErrorMessage}");
			BurnFailed?.Invoke(result.ErrorMessage);
		}
	}

	private bool TryBuildBurnDraft(out BurnDraft draft, out string error)
	{
		draft = new BurnDraft();
		error = "";

		if (_selectedBurnToken == null)
		{
			error = "No token selected.";
			return false;
		}

		string quantityText = _burnQuantityInput.Text.Trim();

		draft = new BurnDraft
		{
			Quantity = quantityText,
			DisplaySymbol = _selectedBurnToken.Symbol,
			TokenInstance = new GalaTokenInstance
			{
				collection = _selectedBurnToken.Collection,
				category = _selectedBurnToken.Category,
				type = _selectedBurnToken.Type,
				additionalKey = _selectedBurnToken.AdditionalKey,
				instance = _selectedBurnToken.Instance
			}
		};

		var result = _walletService!.ValidateBurn(draft, _selectedBurnToken.AvailableAmount);
		if (!result.IsValid)
		{
			error = result.Error;
			return false;
		}

		return true;
	}
}
