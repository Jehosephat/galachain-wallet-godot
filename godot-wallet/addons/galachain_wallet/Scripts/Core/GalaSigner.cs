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

		request.signature = SignPayload(payloadToSign, privateKey);
	}

	public void SignBurn(GalaBurnTokensRequest request, string privateKey)
	{
		var payloadToSign = new
		{
			tokenInstances = request.tokenInstances,
			uniqueKey = request.uniqueKey,
			dtoExpiresAt = request.dtoExpiresAt
		};

		request.signature = SignPayload(payloadToSign, privateKey);
	}

	public void SignGrantAllowance(GalaGrantAllowanceRequest request, string privateKey)
	{
		var payloadToSign = new
		{
			allowanceType = request.allowanceType,
			dtoExpiresAt = request.dtoExpiresAt,
			expires = request.expires,
			quantities = request.quantities,
			tokenInstance = request.tokenInstance,
			uniqueKey = request.uniqueKey,
			uses = request.uses
		};

		request.signature = SignPayload(payloadToSign, privateKey);
	}

	/// <summary>
	/// Signs an arbitrary message using EIP-191 personal_sign format.
	/// The message is prefixed with "\x19Ethereum Signed Message:\n{length}" before hashing,
	/// which prevents the signature from being replayed as a transaction.
	/// Used for authentication challenges (wallet login).
	/// </summary>
	public string SignMessage(string message, string privateKey)
	{
		var signer = new EthereumMessageSigner();
		var key = new EthECKey(privateKey);
		return signer.EncodeUTF8AndSign(message, key);
	}

	private static string SignPayload(object payload, string privateKey)
	{
		string canonicalJson = GalaCanonicalJson.Serialize(payload);
		byte[] hash = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(canonicalJson));

		var key = new EthECKey(privateKey);
		var signature = key.SignAndCalculateV(hash);
		string signatureHex = EthECDSASignature.CreateStringSignature(signature);

		VerifySignature(hash, signature, key.GetPublicAddress());
		return signatureHex;
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
