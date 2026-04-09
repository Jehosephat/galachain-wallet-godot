using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class GalaTransferClient : IGalaTransferClient
{
	private static readonly HttpClient Http = new HttpClient();
	private readonly GalaChainNetworkConfig _config;

	public GalaTransferClient(GalaChainNetworkConfig? config = null)
	{
		_config = config ?? new GalaChainNetworkConfig();
	}

	public async Task<NetworkResult<string>> TransferAsync(GalaTransferTokenRequest request, string walletAlias)
	{
		string json = JsonSerializer.Serialize(request);

		try
		{
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.TransferTokenUrl);
			httpRequest.Content = content;

			using var response = await Http.SendAsync(httpRequest);
			string body = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
				return NetworkResult<string>.Rejected(body, (int)response.StatusCode);

			return NetworkResult<string>.Success(body);
		}
		catch (HttpRequestException ex)
		{
			return NetworkResult<string>.TransportError(ex.Message);
		}
	}
}
