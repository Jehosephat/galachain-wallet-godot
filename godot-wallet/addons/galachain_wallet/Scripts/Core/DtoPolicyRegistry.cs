using System;
using System.Collections.Generic;

namespace GalaWallet.Core;

public class DtoPolicyRegistry
{
	private readonly Dictionary<string, ITransactionPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

	public DtoPolicyRegistry()
	{
		Register(new TransferTokenPolicy());
		Register(new BurnTokensPolicy());
		Register(new GrantAllowancePolicy());
	}

	public void Register(ITransactionPolicy policy)
	{
		_policies[policy.OperationId] = policy;
	}

	public bool IsSupported(string operationId)
	{
		return _policies.ContainsKey(operationId);
	}

	public ValidationResult Validate(string operationId, TransactionContext context)
	{
		if (!_policies.TryGetValue(operationId, out var policy))
			return ValidationResult.Fail($"Unsupported operation: {operationId}");

		return policy.Validate(context);
	}
}
