using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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

	public static NetworkResult<string> ParseTransferResponse(string responseBody)
	{
		GalaChainResponse? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<GalaChainResponse>(
				responseBody,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		}
		catch
		{
			return NetworkResult<string>.ParseError("Could not parse transfer response.");
		}

		if (parsed == null)
			return NetworkResult<string>.ParseError("Could not parse transfer response.");

		if (parsed.Status != 1)
			return NetworkResult<string>.Rejected(parsed.Message);

		return NetworkResult<string>.Success(responseBody);
	}

	public async Task<NetworkResult<string>> TransferAsync(GalaTransferTokenRequest request, string walletAlias)
	{
		string json = JsonSerializer.Serialize(request);

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.WriteTimeoutSeconds));
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.TransferTokenUrl);
			httpRequest.Content = content;

			using var response = await Http.SendAsync(httpRequest, cts.Token);
			string body = await response.Content.ReadAsStringAsync(cts.Token);

			if (!response.IsSuccessStatusCode)
				return NetworkResult<string>.Rejected(body, (int)response.StatusCode);

			return ParseTransferResponse(body);
		}
		catch (TaskCanceledException)
		{
			return NetworkResult<string>.TransportError($"Request timed out after {_config.WriteTimeoutSeconds}s.");
		}
		catch (HttpRequestException ex)
		{
			return NetworkResult<string>.TransportError(ex.Message);
		}
	}
}
