using System.Text;
using Nethereum.Signer;
using Nethereum.Util;

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
	}
}
