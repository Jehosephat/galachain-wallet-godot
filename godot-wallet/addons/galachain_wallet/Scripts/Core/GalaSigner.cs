using System;
using System.Text;
using Nethereum.Signer;
using Nethereum.Util;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class GalaSigner
{
	public void SignTransfer(GalaTransferTokenRequest request, string privateKey)
	{
		var payloadToSign = new
		{
			from = request.from,
			to = request.to,
			tokenInstance = request.tokenInstance,
			quantity = request.quantity,
			uniqueKey = request.uniqueKey,
			dtoExpiresAt = request.dtoExpiresAt
		};

		string canonicalJson = GalaCanonicalJson.Serialize(payloadToSign);
		byte[] hash = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(canonicalJson));

		var key = new EthECKey(privateKey);
		var signature = key.SignAndCalculateV(hash);

		request.signature = EthECDSASignature.CreateStringSignature(signature);

		VerifySignature(hash, signature, key.GetPublicAddress());
	}

	private static void VerifySignature(byte[] hash, EthECDSASignature signature, string expectedAddress)
	{
		var recoveredKey = EthECKey.RecoverFromSignature(signature, hash);
		string recoveredAddress = recoveredKey.GetPublicAddress();

		if (!string.Equals(recoveredAddress, expectedAddress, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"Signature verification failed: recovered address {recoveredAddress} does not match expected {expectedAddress}.");
		}
	}
}
