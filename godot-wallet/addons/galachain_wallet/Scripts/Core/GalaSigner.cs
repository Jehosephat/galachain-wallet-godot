using System;
using System.Text;
using Nethereum.Signer;
using Nethereum.Util;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class GalaSigner
{
	/// <summary>
	/// Signs any object using canonical JSON serialization + keccak256 + secp256k1.
	/// GalaCanonicalJson automatically excludes signature/trace fields.
	/// Returns the hex signature string.
	/// </summary>
	public string Sign(object payload, string privateKey)
	{
		string canonicalJson = GalaCanonicalJson.Serialize(payload);
		byte[] hash = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(canonicalJson));

		var key = new EthECKey(privateKey);
		var signature = key.SignAndCalculateV(hash);
		string signatureHex = EthECDSASignature.CreateStringSignature(signature);

		VerifySignature(hash, signature, key.GetPublicAddress());

		return signatureHex;
	}

	public void SignTransfer(GalaTransferTokenRequest request, string privateKey)
	{
		request.signature = Sign(request, privateKey);
	}

	public void SignMint(GalaMintTokenRequest request, string privateKey)
	{
		request.signature = Sign(request, privateKey);
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
