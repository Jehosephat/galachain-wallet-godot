namespace GalaWallet.Models;

public class WalletSecretPayload
{
	public WalletSecretType SecretType { get; set; }
	public string Secret { get; set; } = "";
}
