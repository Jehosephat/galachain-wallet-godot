using System;
using System.Text;
using Nethereum.Signer;
using Nethereum.Util;

public class GalaSigner
{
	public void SignTransfer(GalaTransferTokenRequest request, string privateKey)
	{
		request.Signature = "";
		request.SignerPublicKey = "";

		string canonicalJson = GalaCanonicalJson.Serialize(request);
		byte[] hash = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(canonicalJson));

		var key = new EthECKey(privateKey);
		var signature = key.SignAndCalculateV(hash);

		// DER + base64 is what GalaConnect documents for write signatures.
		byte[] der = signature.ToDER();
		request.Signature = Convert.ToBase64String(der);

		// Gala expects base64 public key. This is the practical format to emit.
		byte[] pub = key.GetPubKey(true);
		request.SignerPublicKey = Convert.ToBase64String(pub);
	}
}
