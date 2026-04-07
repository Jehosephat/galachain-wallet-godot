namespace GalaWallet.Models;

public class GalaChainNetworkConfig
{
	public string ApiBaseUrl { get; set; } = "https://gateway-mainnet.galachain.com/api";
	public string Channel { get; set; } = "asset";
	public string Contract { get; set; } = "token-contract";

	public string FetchBalancesUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/FetchBalances";
	public string TransferTokenUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/TransferToken";
	public string DryRunUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/DryRun";
}
