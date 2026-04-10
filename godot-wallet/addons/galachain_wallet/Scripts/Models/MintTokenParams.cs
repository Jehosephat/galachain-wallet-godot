namespace GalaWallet.Models;

/// <summary>
/// Game-facing parameters for minting a token.
/// The game provides these; GameOperations handles DTO building, signing, and submission.
/// </summary>
public class MintTokenParams
{
	/// <summary>Token collection name (e.g., "MyGame").</summary>
	public string Collection { get; set; } = "";

	/// <summary>Token category (e.g., "Weapon", "Building").</summary>
	public string Category { get; set; } = "";

	/// <summary>Token type (e.g., "Sword", "FruitsGreenhouse").</summary>
	public string Type { get; set; } = "";

	/// <summary>Additional key (e.g., "Epic", "Legendary", or "none").</summary>
	public string AdditionalKey { get; set; } = "none";

	/// <summary>Recipient address in GalaChain format (e.g., "eth|abc..." or "client|...").</summary>
	public string Owner { get; set; } = "";

	/// <summary>Number of tokens to mint. For NFTs, each unit creates a unique instance.</summary>
	public string Quantity { get; set; } = "1";
}
