public class GalaChainNetworkConfig
{
	public string ApiBaseUrl { get; set; } = "https://gateway-mainnet.galachain.com/api";
	public string Channel { get; set; } = "asset";
	public string Contract { get; set; } = "token-contract";

	public string FetchBalancesUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/FetchBalances";
}
