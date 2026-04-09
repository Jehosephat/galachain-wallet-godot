using System;
using System.Globalization;

namespace GalaWallet.Core;

public class TransferTokenPolicy : ITransactionPolicy
{
	public string OperationId => "TransferToken";

	public ValidationResult Validate(TransactionContext context)
	{
		if (string.IsNullOrWhiteSpace(context.ToAddress))
			return ValidationResult.Fail("Recipient address is required.");

		if (!context.ToAddress.StartsWith("eth|", StringComparison.OrdinalIgnoreCase)
			&& !context.ToAddress.StartsWith("client|", StringComparison.OrdinalIgnoreCase))
			return ValidationResult.Fail("Recipient address must start with eth| or client|.");

		if (string.Equals(context.ToAddress, context.FromAddress, StringComparison.OrdinalIgnoreCase))
			return ValidationResult.Fail("Cannot transfer to the same wallet address.");

		if (string.IsNullOrWhiteSpace(context.Quantity))
			return ValidationResult.Fail("Quantity is required.");

		if (!decimal.TryParse(context.Quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity))
			return ValidationResult.Fail("Quantity must be a valid number.");

		if (quantity <= 0m)
			return ValidationResult.Fail("Quantity must be greater than zero.");

		if (quantity > context.AvailableBalance)
			return ValidationResult.Fail("Quantity exceeds available balance.");

		return ValidationResult.Ok();
	}
}
