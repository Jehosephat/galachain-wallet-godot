namespace GalaWallet.Models;

public class GalaChainNetworkConfig
{
	public string ApiBaseUrl { get; set; } = "https://gateway-mainnet.galachain.com/api";
	public string Channel { get; set; } = "asset";
	public string Contract { get; set; } = "token-contract";

	public int ReadTimeoutSeconds { get; set; } = 15;
	public int WriteTimeoutSeconds { get; set; } = 30;

	public string FetchBalancesUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/FetchBalancesWithTokenMetadata";
	public string TransferTokenUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/TransferToken";
	public string DryRunUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/DryRun";
	public string BurnTokensUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/BurnTokens";
}
