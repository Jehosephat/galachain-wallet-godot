using System;
using System.Globalization;

namespace GalaWallet.Core;

public class MintTokenPolicy : ITransactionPolicy
{
	public string OperationId => "MintToken";

	public ValidationResult Validate(TransactionContext context)
	{
		if (string.IsNullOrWhiteSpace(context.ToAddress))
			return ValidationResult.Fail("Owner address is required.");

		if (!context.ToAddress.StartsWith("eth|", StringComparison.OrdinalIgnoreCase)
			&& !context.ToAddress.StartsWith("client|", StringComparison.OrdinalIgnoreCase))
			return ValidationResult.Fail("Owner address must start with eth| or client|.");

		if (string.IsNullOrWhiteSpace(context.Quantity))
			return ValidationResult.Fail("Quantity is required.");

		if (!decimal.TryParse(context.Quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity))
			return ValidationResult.Fail("Quantity must be a valid number.");

		if (quantity <= 0m)
			return ValidationResult.Fail("Quantity must be greater than zero.");

		return ValidationResult.Ok();
	}
}
