using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

	public async Task<List<TokenBalanceModel>> FetchBalancesAsync(string ethAddress)
	{
		if (string.IsNullOrWhiteSpace(ethAddress))
			throw new InvalidOperationException("Cannot fetch balances without an address.");

		string owner = BuildOwnerAlias(ethAddress);

		var request = new GalaFetchBalancesRequest
		{
			Owner = owner
		};

		string json = JsonSerializer.Serialize(request);
		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var response = await Http.PostAsync(_config.FetchBalancesUrl, content);

		string responseText = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"FetchBalances failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseText}");
		}

		var parsed = JsonSerializer.Deserialize<GalaFetchBalancesResponse>(
			responseText,
			new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

		if (parsed == null)
			throw new InvalidOperationException("FetchBalances response could not be parsed.");

		var results = new List<TokenBalanceModel>();

		foreach (var item in parsed.Data)
		{
			decimal total = ParseDecimal(item.Quantity);
			decimal locked = 0m;

			foreach (var hold in item.LockedHolds)
			{
				locked += ParseDecimal(hold.Quantity);
			}

			decimal available = total - locked;
			if (available < 0m)
				available = 0m;

			string symbol = BuildDisplaySymbol(item);

			results.Add(new TokenBalanceModel
			{
				Symbol = symbol,
				DisplayAmount = locked > 0m
					? $"{available:0.########} available ({total:0.########} total)"
					: $"{total:0.########}",

				Collection = item.Collection,
				Category = item.Category,
				Type = item.Type,
				AdditionalKey = item.AdditionalKey,
				Instance = "0",
				RawQuantity = item.Quantity,
				AvailableAmount = available
			});
		}

		return results;
	}

	public async Task<TransferPreviewResult> DryRunTransferAsync(GalaTransferTokenRequest signedRequest)
	{
		var wrapper = new
		{
			method = "TransferToken",
			signerAddress = signedRequest.from,
			dto = new
			{
				signedRequest.from,
				signedRequest.to,
				signedRequest.tokenInstance,
				signedRequest.quantity,
				uniqueKey = $"dryrun-{System.Guid.NewGuid()}",
				signedRequest.dtoExpiresAt
			}
		};

		string json = JsonSerializer.Serialize(wrapper);

		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var response = await Http.PostAsync(_config.DryRunUrl, content);

		string responseText = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			return new TransferPreviewResult
			{
				WouldSucceed = false,
				Message = $"Dry run request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseText}"
			};
		}

		var parsed = JsonSerializer.Deserialize<GalaDryRunResponse>(
			responseText,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		if (parsed == null)
		{
			return new TransferPreviewResult
			{
				WouldSucceed = false,
				Message = "Could not parse dry run response."
			};
		}

		var inner = parsed.Data.Response;
		bool success = parsed.Status == 1 && inner.Status == 1;
		string fee = ExtractFeeFromWrites(parsed.Data.Writes, "TransferToken", signedRequest.from);

		return new TransferPreviewResult
		{
			WouldSucceed = success,
			Message = success ? (inner.Message ?? "OK") : (inner.Message ?? parsed.Message),
			EstimatedFee = fee,
			FeeToken = "GALA"
		};
	}

	private static string ExtractFeeFromWrites(Dictionary<string, string> writes, string method, string userAddress)
	{
		// Fee record key pattern: \0GCFR\0{year}\0{month}\0{day}\0{method}\0{userAddress}\0{txId}\0
		// Each GCFR entry has a "quantity" field with the fee for that transaction.
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
		{
			throw new InvalidOperationException("Expected an Ethereum-style address beginning with 0x.");
		}

		string withoutPrefix = trimmed[2..];
		return $"eth|{withoutPrefix}";
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
