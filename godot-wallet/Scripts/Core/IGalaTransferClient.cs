using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class GalaTransferClient : IGalaTransferClient
{
	private static readonly HttpClient Http = new HttpClient();
	private readonly GalaChainNetworkConfig _config;

	public GalaTransferClient(GalaChainNetworkConfig? config = null)
	{
		_config = config ?? new GalaChainNetworkConfig();
	}

	public async Task<string> TransferAsync(GalaTransferTokenRequest request, string walletAlias)
	{
		string url = $"{_config.ApiBaseUrl}/{_config.Channel}/{_config.Contract}/TransferToken";
		string json = JsonSerializer.Serialize(request);

		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
		httpRequest.Content = content;

		// Add only if your endpoint expects it; GalaConnect documents this header for write calls.
		// httpRequest.Headers.Add("X-Wallet-Address", walletAlias);

		using var response = await Http.SendAsync(httpRequest);
		string body = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"Transfer failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
		}

		return body;
	}
}
