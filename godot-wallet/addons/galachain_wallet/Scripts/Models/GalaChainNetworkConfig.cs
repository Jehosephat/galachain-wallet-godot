namespace GalaWallet.Models;

public class GalaChainNetworkConfig
{
	public const string MainnetUrl = "https://gateway-mainnet.galachain.com/api";
	public const string TestnetUrl = "https://gateway-testnet.galachain.com/api";

	public string ApiBaseUrl { get; set; } = MainnetUrl;
	public string Channel { get; set; } = "asset";
	public string Contract { get; set; } = "token-contract";

	public int ReadTimeoutSeconds { get; set; } = 15;
	public int WriteTimeoutSeconds { get; set; } = 30;

	public string FetchBalancesUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/FetchBalancesWithTokenMetadata";
	public string TransferTokenUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/TransferToken";
	public string DryRunUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/DryRun";
	public string BurnTokensUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/BurnTokens";
	public string GrantAllowanceUrl => $"{ApiBaseUrl}/{Channel}/{Contract}/GrantAllowance";

	/// <summary>Default GalaChain mainnet configuration.</summary>
	public static GalaChainNetworkConfig Mainnet() => new()
	{
		ApiBaseUrl = MainnetUrl,
		Channel = "asset",
		Contract = "token-contract"
	};

	/// <summary>
	/// Default GalaChain testnet configuration.
	/// Testnet contract deployments use channel "testnet01" and a contract name
	/// derived from the deployment hash (e.g., "gc-a9b8b472...-GalaChainToken").
	/// Update the Contract value here if the testnet deployment changes.
	/// </summary>
	public static GalaChainNetworkConfig Testnet() => new()
	{
		ApiBaseUrl = TestnetUrl,
		Channel = "testnet01",
		Contract = "gc-a9b8b472b035c0510508c248d1110d3162b7e5f4-GalaChainToken"
	};
}
