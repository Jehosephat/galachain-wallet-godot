public class GalaTransferTokenRequest
{
	public string SignerPublicKey { get; set; } = "";
	public string Signature { get; set; } = "";
	public string UniqueKey { get; set; } = "";
	public long DtoExpiresAt { get; set; }

	public string From { get; set; } = "";
	public string To { get; set; } = "";
	public GalaTokenInstance TokenInstance { get; set; } = new();
	public string Quantity { get; set; } = "";
}
