using System.Globalization;

namespace GalaWallet.Core;

public class BurnTokensPolicy : ITransactionPolicy
{
	public string OperationId => "BurnTokens";

	public ValidationResult Validate(TransactionContext context)
	{
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
