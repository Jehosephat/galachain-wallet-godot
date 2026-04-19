using Godot;
using System;
using System.Collections.Generic;
using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.UI;

public partial class GalaChainWallet
{
	private void OnGrantAllowancePressed()
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet() || !walletService.IsUnlocked())
		{
			Log("Unlock the wallet before granting an allowance.");
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

		OpenGrantAllowanceDialog(balances[selectedIndex], "", "", AllowanceType.Transfer, 0L, readOnly: false);
	}

	/// <summary>
	/// Game-initiated allowance grant. Opens a pre-filled confirmation dialog for
	/// the player to approve. If the wallet is locked, prompts unlock first.
	/// </summary>
	public void RequestGrantAllowance(string spender, string quantity, string tokenSymbol, AllowanceType type, int expiresInDays)
	{
		if (!EnsureService())
		{
			return;
		}

		var walletService = _walletService!;

		if (!walletService.HasWallet())
		{
			Log("Create or import a wallet before granting an allowance.");
			return;
		}

		long expiresUnixMs = expiresInDays > 0
			? DateTimeOffset.UtcNow.AddDays(expiresInDays).ToUnixTimeMilliseconds()
			: 0L;

		if (!walletService.IsUnlocked())
		{
			_pendingGrantAllowanceSpender = spender;
			_pendingGrantAllowanceQuantity = quantity;
			_pendingGrantAllowanceSymbol = tokenSymbol;
			_pendingGrantAllowanceType = type;
			_pendingGrantAllowanceExpiresUnixMs = expiresUnixMs;
			Log("Unlock the wallet to complete the allowance grant.");
			OpenPasswordDialog(
				PendingPasswordAction.UnlockWallet,
				"Enter your wallet password to unlock."
			);
			return;
		}

		ExecuteGrantAllowanceRequest(spender, quantity, tokenSymbol, type, expiresUnixMs);
	}

	private void ExecuteGrantAllowanceRequest(string spender, string quantity, string tokenSymbol, AllowanceType type, long expiresUnixMs)
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

		OpenGrantAllowanceDialog(match, spender, quantity, type, expiresUnixMs, readOnly: true);
	}

	private void ConsumePendingGrantAllowance()
	{
		if (_pendingGrantAllowanceSpender == null)
		{
			return;
		}

		string spender = _pendingGrantAllowanceSpender;
		string quantity = _pendingGrantAllowanceQuantity ?? "";
		string symbol = _pendingGrantAllowanceSymbol ?? "";
		AllowanceType type = _pendingGrantAllowanceType;
		long expiresUnixMs = _pendingGrantAllowanceExpiresUnixMs;

		_pendingGrantAllowanceSpender = null;
		_pendingGrantAllowanceQuantity = null;
		_pendingGrantAllowanceSymbol = null;
		_pendingGrantAllowanceExpiresUnixMs = 0L;

		ExecuteGrantAllowanceRequest(spender, quantity, symbol, type, expiresUnixMs);
	}

	private void OpenGrantAllowanceDialog(TokenBalanceModel token, string spender, string quantity, AllowanceType type, long expiresUnixMs, bool readOnly)
	{
		ResetIdleTimer();
		_selectedGrantAllowanceToken = token;

		_grantAllowanceSelectedTokenLabel.Text =
			$"Token: {token.Symbol} | Available: {token.AvailableAmount:0.########}";

		_grantAllowanceTypeOption.Clear();
		_grantAllowanceTypeOption.AddItem("Transfer", (int)AllowanceType.Transfer);
		_grantAllowanceTypeOption.AddItem("Burn", (int)AllowanceType.Burn);
		int typeIndex = type == AllowanceType.Burn ? 1 : 0;
		_grantAllowanceTypeOption.Selected = typeIndex;

		_grantAllowanceSpenderInput.Text = spender;
		_grantAllowanceQuantityInput.Text = quantity;
		_grantAllowanceExpiresInput.Text = FormatExpiresInput(expiresUnixMs);

		_grantAllowanceTypeOption.Disabled = readOnly;
		_grantAllowanceSpenderLabel.Visible = !readOnly;
		_grantAllowanceSpenderInput.Visible = !readOnly;
		_grantAllowanceQuantityLabel.Visible = !readOnly;
		_grantAllowanceQuantityInput.Visible = !readOnly;

		UpdateGrantAllowanceSummary();

		_grantAllowanceDialog.PopupCentered(new Vector2I(520, 240));
	}

	private static string FormatExpiresInput(long expiresUnixMs)
	{
		if (expiresUnixMs <= 0)
			return "";

		long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		double days = (expiresUnixMs - nowMs) / (1000.0 * 60 * 60 * 24);

		if (days <= 0)
			return "";

		return Math.Max(1, (int)Math.Ceiling(days)).ToString();
	}

	private void OnGrantAllowanceInputChanged(string _newText)
	{
		UpdateGrantAllowanceSummary();
	}

	private void UpdateGrantAllowanceSummary()
	{
		if (!TryBuildGrantAllowanceDraft(out var draft, out var error))
		{
			_grantAllowanceSummaryLabel.Text = string.IsNullOrWhiteSpace(error)
				? ""
				: $"Preview unavailable: {error}";
			return;
		}

		_grantAllowanceSummaryLabel.Text = BuildGrantAllowanceSummary(draft, "loading...");
		RunGrantAllowanceDryRunPreview(draft);
	}

	private static string BuildGrantAllowanceSummary(GrantAllowanceDraft draft, string feeDisplay)
	{
		var spender = draft.Spenders[0];
		string expiresText = draft.ExpiresUnixMs == 0
			? "never"
			: DateTimeOffset.FromUnixTimeMilliseconds(draft.ExpiresUnixMs).ToString("yyyy-MM-dd 'UTC'");

		return
			$"Grant {spender.Quantity} {draft.DisplaySymbol} {draft.AllowanceType} allowance to:\n" +
			$"{spender.User}\n" +
			$"Expires {expiresText} · Fee: {feeDisplay}";
	}

	private async void RunGrantAllowanceDryRunPreview(GrantAllowanceDraft draft)
	{
		if (_walletService == null || !_walletService.IsUnlocked())
			return;

		var result = await _walletService.PreviewGrantAllowanceAsync(draft);

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

		_grantAllowanceSummaryLabel.Text = BuildGrantAllowanceSummary(draft, feeDisplay);
	}

	private async void OnGrantAllowanceDialogConfirmed()
	{
		if (!EnsureService())
		{
			return;
		}

		ResetIdleTimer();
		var walletService = _walletService!;

		if (!TryBuildGrantAllowanceDraft(out var draft, out var error))
		{
			Log($"Allowance grant failed: {error}");
			UpdateGrantAllowanceSummary();
			return;
		}

		var spender = draft.Spenders[0];
		Log($"Submitting {draft.AllowanceType} allowance of {spender.Quantity} {draft.DisplaySymbol} to {spender.User}...");

		var result = await walletService.SubmitGrantAllowanceAsync(draft);
		RefreshUi();

		if (result.IsSuccess)
		{
			Log("Allowance granted.");
			AllowanceGranted?.Invoke(spender.User, spender.Quantity, draft.DisplaySymbol, draft.AllowanceType.ToString());
			BalancesRefreshed?.Invoke();
		}
		else
		{
			Log($"Allowance grant failed: {result.ErrorMessage}");
			AllowanceGrantFailed?.Invoke(result.ErrorMessage);
		}
	}

	private bool TryBuildGrantAllowanceDraft(out GrantAllowanceDraft draft, out string error)
	{
		draft = new GrantAllowanceDraft();
		error = "";

		if (_selectedGrantAllowanceToken == null)
		{
			error = "No token selected.";
			return false;
		}

		int selectedId = _grantAllowanceTypeOption.GetItemId(_grantAllowanceTypeOption.Selected);
		AllowanceType type = (AllowanceType)selectedId;

		string spenderText = _grantAllowanceSpenderInput.Text.Trim();
		string quantityText = _grantAllowanceQuantityInput.Text.Trim();
		string expiresText = _grantAllowanceExpiresInput.Text.Trim();

		long expiresUnixMs = 0L;
		if (!string.IsNullOrWhiteSpace(expiresText))
		{
			if (!int.TryParse(expiresText, out int days) || days < 0)
			{
				error = "Expiration must be a whole number of days, or blank for no expiration.";
				return false;
			}

			if (days > 0)
			{
				expiresUnixMs = DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeMilliseconds();
			}
		}

		draft = new GrantAllowanceDraft
		{
			AllowanceType = type,
			Spenders = new List<GrantAllowanceSpender>
			{
				new GrantAllowanceSpender { User = spenderText, Quantity = quantityText }
			},
			ExpiresUnixMs = expiresUnixMs,
			DisplaySymbol = _selectedGrantAllowanceToken.Symbol,
			TokenInstance = new GalaTokenInstance
			{
				collection = _selectedGrantAllowanceToken.Collection,
				category = _selectedGrantAllowanceToken.Category,
				type = _selectedGrantAllowanceToken.Type,
				additionalKey = _selectedGrantAllowanceToken.AdditionalKey,
				instance = _selectedGrantAllowanceToken.Instance
			}
		};

		var result = _walletService!.ValidateGrantAllowance(draft, _selectedGrantAllowanceToken.AvailableAmount);
		if (!result.IsValid)
		{
			error = result.Error;
			return false;
		}

		return true;
	}
}
