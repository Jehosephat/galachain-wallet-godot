using System;
using System.Globalization;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class GrantAllowancePolicy : ITransactionPolicy
{
	public string OperationId => "GrantAllowance";

	public ValidationResult Validate(TransactionContext context)
	{
		if (context.AllowanceType != (int)AllowanceType.Transfer
			&& context.AllowanceType != (int)AllowanceType.Burn)
			return ValidationResult.Fail("Only Transfer and Burn allowances can be granted from this wallet.");

		if (string.IsNullOrWhiteSpace(context.ToAddress))
			return ValidationResult.Fail("Spender address is required.");

		if (!context.ToAddress.StartsWith("eth|", StringComparison.OrdinalIgnoreCase)
			&& !context.ToAddress.StartsWith("client|", StringComparison.OrdinalIgnoreCase))
			return ValidationResult.Fail("Spender address must start with eth| or client|.");

		if (string.Equals(context.ToAddress, context.FromAddress, StringComparison.OrdinalIgnoreCase))
			return ValidationResult.Fail("Cannot grant an allowance to your own wallet.");

		if (string.IsNullOrWhiteSpace(context.Quantity))
			return ValidationResult.Fail("Quantity is required.");

		if (!decimal.TryParse(context.Quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity))
			return ValidationResult.Fail("Quantity must be a valid number.");

		if (quantity <= 0m)
			return ValidationResult.Fail("Quantity must be greater than zero.");

		if (context.ExpiresUnixMs != 0 && context.ExpiresUnixMs <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
			return ValidationResult.Fail("Expiration must be in the future, or 0 for no expiration.");

		return ValidationResult.Ok();
	}
}
