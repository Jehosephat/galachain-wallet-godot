namespace GalaWallet.Models;

public class TransferPreviewResult
{
	public bool WouldSucceed { get; set; }
	public string Message { get; set; } = "";
	public string EstimatedFee { get; set; } = "0";
	public string FeeToken { get; set; } = "";
}
