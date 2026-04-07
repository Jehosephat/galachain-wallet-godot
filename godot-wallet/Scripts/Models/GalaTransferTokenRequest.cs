public class GalaTransferTokenRequest
{
	public string from { get; set; } = "";
	public string to { get; set; } = "";
	public GalaTokenInstance tokenInstance { get; set; } = new();
	public string quantity { get; set; } = "";
	public string uniqueKey { get; set; } = "";
	public long dtoExpiresAt { get; set; }
	public string signature { get; set; } = "";
}
