namespace GalaWallet.Models;

public class EncryptedWalletRecord
{
	public int Version { get; set; } = 1;
	public string Address { get; set; } = "";
	public WalletSecretType SecretType { get; set; }

	public int KdfIterations { get; set; } = 100_000;

	public string SaltBase64 { get; set; } = "";
	public string NonceBase64 { get; set; } = "";
	public string TagBase64 { get; set; } = "";
	public string CipherTextBase64 { get; set; } = "";
}
