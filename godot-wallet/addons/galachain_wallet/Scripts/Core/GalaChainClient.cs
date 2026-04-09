using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class GalaChainClient : IGalaChainClient
{
	private static readonly HttpClient Http = new HttpClient();
	private readonly GalaChainNetworkConfig _config;

	public GalaChainClient(GalaChainNetworkConfig? config = null)
	{
		_config = config ?? new GalaChainNetworkConfig();
	}

	public async Task<NetworkResult<List<TokenBalanceModel>>> FetchBalancesAsync(string ethAddress)
	{
		if (string.IsNullOrWhiteSpace(ethAddress))
			return NetworkResult<List<TokenBalanceModel>>.Rejected("Cannot fetch balances without an address.");

		string owner = BuildOwnerAlias(ethAddress);

		var request = new GalaFetchBalancesRequest
		{
			Owner = owner
		};

		string json = JsonSerializer.Serialize(request);

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ReadTimeoutSeconds));
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var response = await Http.PostAsync(_config.FetchBalancesUrl, content, cts.Token);

			string responseText = await response.Content.ReadAsStringAsync(cts.Token);

			if (!response.IsSuccessStatusCode)
				return NetworkResult<List<TokenBalanceModel>>.Rejected(responseText, (int)response.StatusCode);

			var parsed = JsonSerializer.Deserialize<GalaFetchBalancesResponse>(
				responseText,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (parsed == null)
				return NetworkResult<List<TokenBalanceModel>>.ParseError("FetchBalances response could not be parsed.");

			var results = new List<TokenBalanceModel>();

			foreach (var entry in parsed.Data.Results)
			{
				var bal = entry.Balance;
				var meta = entry.Token;

				decimal total = ParseDecimal(bal.Quantity);
				decimal locked = 0m;

				foreach (var hold in bal.LockedHolds)
				{
					locked += ParseDecimal(hold.Quantity);
				}

				decimal available = total - locked;
				if (available < 0m)
					available = 0m;

				string symbol = !string.IsNullOrWhiteSpace(meta.Symbol)
					? meta.Symbol
					: BuildDisplaySymbol(bal);

				results.Add(new TokenBalanceModel
				{
					Symbol = symbol,
					DisplayAmount = locked > 0m
						? $"{available:0.########} available ({total:0.########} total)"
						: $"{total:0.########}",

					Collection = bal.Collection,
					Category = bal.Category,
					Type = bal.Type,
					AdditionalKey = bal.AdditionalKey,
					Instance = "0",
					RawQuantity = bal.Quantity,
					AvailableAmount = available,
					ImageUrl = meta.Image
				});
			}

			return NetworkResult<List<TokenBalanceModel>>.Success(results);
		}
		catch (TaskCanceledException)
		{
			return NetworkResult<List<TokenBalanceModel>>.TransportError($"Request timed out after {_config.ReadTimeoutSeconds}s.");
		}
		catch (HttpRequestException ex)
		{
			return NetworkResult<List<TokenBalanceModel>>.TransportError(ex.Message);
		}
	}

	public async Task<NetworkResult<TransferPreviewResult>> DryRunTransferAsync(GalaTransferTokenRequest request)
	{
		var wrapper = new
		{
			method = "TransferToken",
			signerAddress = request.from,
			dto = new
			{
				request.from,
				request.to,
				request.tokenInstance,
				request.quantity,
				uniqueKey = $"dryrun-{Guid.NewGuid()}",
				request.dtoExpiresAt
			}
		};

		string json = JsonSerializer.Serialize(wrapper);

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ReadTimeoutSeconds));
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var response = await Http.PostAsync(_config.DryRunUrl, content, cts.Token);

			string responseText = await response.Content.ReadAsStringAsync(cts.Token);

			if (!response.IsSuccessStatusCode)
			{
				return NetworkResult<TransferPreviewResult>.Rejected(
					$"{(int)response.StatusCode}: {responseText}", (int)response.StatusCode);
			}

			var parsed = JsonSerializer.Deserialize<GalaDryRunResponse>(
				responseText,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (parsed == null)
				return NetworkResult<TransferPreviewResult>.ParseError("Could not parse dry run response.");

			var inner = parsed.Data.Response;
			bool success = parsed.Status == 1 && inner.Status == 1;
			string fee = ExtractFeeFromWrites(parsed.Data.Writes, "TransferToken", request.from);

			var preview = new TransferPreviewResult
			{
				WouldSucceed = success,
				Message = success ? (inner.Message ?? "OK") : (inner.Message ?? parsed.Message),
				EstimatedFee = fee,
				FeeToken = "GALA"
			};

			return NetworkResult<TransferPreviewResult>.Success(preview);
		}
		catch (TaskCanceledException)
		{
			return NetworkResult<TransferPreviewResult>.TransportError($"Request timed out after {_config.ReadTimeoutSeconds}s.");
		}
		catch (HttpRequestException ex)
		{
			return NetworkResult<TransferPreviewResult>.TransportError(ex.Message);
		}
	}

	private static string ExtractFeeFromWrites(Dictionary<string, string> writes, string method, string userAddress)
	{
		decimal totalFee = 0m;

		foreach (var kvp in writes)
		{
			if (!kvp.Key.Contains("GCFR"))
				continue;

			try
			{
				using var doc = JsonDocument.Parse(kvp.Value);
				if (doc.RootElement.TryGetProperty("quantity", out var qtyEl))
				{
					string qty = qtyEl.GetString() ?? qtyEl.GetRawText();
					if (decimal.TryParse(qty, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
					{
						totalFee += amount;
					}
				}
			}
			catch
			{
				// skip unparseable entries
			}
		}

		return totalFee > 0m ? totalFee.ToString(CultureInfo.InvariantCulture) : "0";
	}

	private static string BuildOwnerAlias(string ethAddress)
	{
		string trimmed = ethAddress.Trim();

		if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			return $"eth|{trimmed}";

		return $"eth|{trimmed[2..]}";
	}

	private static string BuildDisplaySymbol(GalaBalanceDto item)
	{
		bool simple =
			item.Category.Equals("Unit", StringComparison.OrdinalIgnoreCase) &&
			item.Type.Equals("none", StringComparison.OrdinalIgnoreCase) &&
			item.AdditionalKey.Equals("none", StringComparison.OrdinalIgnoreCase);

		if (simple)
			return item.Collection;

		return $"{item.Collection}/{item.Category}/{item.Type}/{item.AdditionalKey}";
	}

	private static decimal ParseDecimal(string value)
	{
		if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			return result;

		return 0m;
	}
}
