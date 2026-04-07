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
