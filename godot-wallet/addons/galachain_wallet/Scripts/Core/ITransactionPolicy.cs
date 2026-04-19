using System.Collections.Generic;

namespace GalaWallet.Core;

public interface ITransactionPolicy
{
	string OperationId { get; }
	ValidationResult Validate(TransactionContext context);
}

public class TransactionContext
{
	public string FromAddress { get; set; } = "";
	public string ToAddress { get; set; } = "";
	public string Quantity { get; set; } = "";
	public decimal AvailableBalance { get; set; }
	public int AllowanceType { get; set; } = -1;
	public long ExpiresUnixMs { get; set; }
}

public class ValidationResult
{
	public bool IsValid { get; set; }
	public string Error { get; set; } = "";

	public static ValidationResult Ok() => new() { IsValid = true };
	public static ValidationResult Fail(string error) => new() { IsValid = false, Error = error };
}
