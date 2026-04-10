using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

/// <summary>
/// High-level class for game-side GalaChain operations.
/// Uses the game's own private key (not the player's wallet key).
/// Handles DTO building, signing, and submission to GalaChain.
/// </summary>
public class GameOperations
{
	private static readonly HttpClient Http = new HttpClient();
	private readonly GalaChainNetworkConfig _config;
	private readonly GalaSigner _signer;
	private readonly DtoPolicyRegistry _policyRegistry;

	public GameOperations(
		GalaChainNetworkConfig? config = null,
		GalaSigner? signer = null,
		DtoPolicyRegistry? policyRegistry = null)
	{
		_config = config ?? new GalaChainNetworkConfig();
		_signer = signer ?? new GalaSigner();
		_policyRegistry = policyRegistry ?? new DtoPolicyRegistry();
	}

	/// <summary>
	/// Mints tokens to an owner address using the game's private key.
	/// Builds the DTO, validates it, signs it, and submits to GalaChain.
	/// </summary>
	/// <param name="gamePrivateKey">The game's private key (hex, with or without 0x prefix). Must have minting authority.</param>
	/// <param name="mintParams">What to mint and to whom.</param>
	/// <returns>Result containing the minted token instances on success.</returns>
	public async Task<MintTokenResult> MintTokenAsync(string gamePrivateKey, MintTokenParams mintParams)
	{
		var validation = _policyRegistry.Validate("MintToken", new TransactionContext
		{
			ToAddress = mintParams.Owner,
			Quantity = mintParams.Quantity
		});

		if (!validation.IsValid)
			return new MintTokenResult { Success = false, Message = validation.Error };

		var request = new GalaMintTokenRequest
		{
			tokenClass = new GalaTokenClassKey
			{
				collection = mintParams.Collection,
				category = mintParams.Category,
				type = mintParams.Type,
				additionalKey = mintParams.AdditionalKey
			},
			owner = mintParams.Owner,
			quantity = mintParams.Quantity,
			uniqueKey = $"godot-mint-{Guid.NewGuid()}",
			dtoExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds()
		};

		try
		{
			_signer.SignMint(request, gamePrivateKey);
		}
		catch (Exception ex)
		{
			return new MintTokenResult { Success = false, Message = $"Signing failed: {ex.Message}" };
		}

		return await SubmitMintAsync(request);
	}

	private async Task<MintTokenResult> SubmitMintAsync(GalaMintTokenRequest request)
	{
		string json = JsonSerializer.Serialize(request);

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.WriteTimeoutSeconds));
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var response = await Http.PostAsync(_config.MintTokenUrl, content, cts.Token);

			string body = await response.Content.ReadAsStringAsync(cts.Token);

			if (!response.IsSuccessStatusCode)
				return new MintTokenResult { Success = false, Message = body };

			var parsed = JsonSerializer.Deserialize<GalaChainMintResponse>(
				body,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (parsed == null)
				return new MintTokenResult { Success = false, Message = "Could not parse mint response." };

			if (parsed.Status != 1)
				return new MintTokenResult { Success = false, Message = parsed.Message };

			return new MintTokenResult
			{
				Success = true,
				Message = parsed.Message,
				MintedInstances = parsed.Data ?? new List<MintedTokenInstance>()
			};
		}
		catch (TaskCanceledException)
		{
			return new MintTokenResult { Success = false, Message = $"Request timed out after {_config.WriteTimeoutSeconds}s." };
		}
		catch (HttpRequestException ex)
		{
			return new MintTokenResult { Success = false, Message = ex.Message };
		}
	}
}

internal class GalaChainMintResponse
{
	[System.Text.Json.Serialization.JsonPropertyName("Status")]
	public int Status { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("Message")]
	public string Message { get; set; } = "";

	[System.Text.Json.Serialization.JsonPropertyName("Data")]
	public List<MintedTokenInstance>? Data { get; set; }
}
