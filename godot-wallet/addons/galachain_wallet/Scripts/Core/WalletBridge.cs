using Godot;
using Godot.Collections;
using GalaWallet.Models;

namespace GalaWallet.Core;

/// <summary>
/// GDScript-compatible wrapper around WalletFacade.
/// Registered as an autoload singleton ("Wallet") when the plugin is enabled.
/// C# games can use WalletFacade directly — this bridge is for GDScript interop.
/// </summary>
public partial class WalletBridge : Node
{
	private WalletFacade _facade = null!;

	[Signal] public delegate void WalletCreatedEventHandler(string address);
	[Signal] public delegate void WalletImportedEventHandler(string address);
	[Signal] public delegate void WalletUnlockedEventHandler(string address);
	[Signal] public delegate void WalletLockedEventHandler();
	[Signal] public delegate void TransferCompletedEventHandler(string toAddress, string quantity, string symbol);
	[Signal] public delegate void TransferFailedEventHandler(string error);
	[Signal] public delegate void BurnCompletedEventHandler(string quantity, string symbol);
	[Signal] public delegate void BurnFailedEventHandler(string error);
	[Signal] public delegate void AllowanceGrantedEventHandler(string spender, string quantity, string symbol, string allowanceType);
	[Signal] public delegate void AllowanceGrantFailedEventHandler(string error);
	[Signal] public delegate void MessageSignedEventHandler(string message, string signature, string address);
	[Signal] public delegate void MessageSignDeclinedEventHandler();
	[Signal] public delegate void BalancesRefreshedEventHandler();

	public override void _Ready()
	{
		SetFacade(new WalletFacade());
	}

	private void SetFacade(WalletFacade facade)
	{
		_facade = facade;

		_facade.WalletCreated += addr => EmitSignal(SignalName.WalletCreated, addr);
		_facade.WalletImported += addr => EmitSignal(SignalName.WalletImported, addr);
		_facade.WalletUnlocked += addr => EmitSignal(SignalName.WalletUnlocked, addr);
		_facade.WalletLocked += () => EmitSignal(SignalName.WalletLocked);
		_facade.TransferCompleted += (to, qty, sym) => EmitSignal(SignalName.TransferCompleted, to, qty, sym);
		_facade.TransferFailed += err => EmitSignal(SignalName.TransferFailed, err);
		_facade.BurnCompleted += (qty, sym) => EmitSignal(SignalName.BurnCompleted, qty, sym);
		_facade.BurnFailed += err => EmitSignal(SignalName.BurnFailed, err);
		_facade.AllowanceGranted += (spender, qty, sym, type) => EmitSignal(SignalName.AllowanceGranted, spender, qty, sym, type);
		_facade.AllowanceGrantFailed += err => EmitSignal(SignalName.AllowanceGrantFailed, err);
		_facade.MessageSigned += (msg, sig, addr) => EmitSignal(SignalName.MessageSigned, msg, sig, addr);
		_facade.MessageSignDeclined += () => EmitSignal(SignalName.MessageSignDeclined);
		_facade.BalancesRefreshed += () => EmitSignal(SignalName.BalancesRefreshed);
	}

	/// <summary>
	/// Switches the wallet to GalaChain testnet. Call this in _Ready() of your
	/// main scene BEFORE any wallet operations (OpenWallet, RefreshBalances, etc.).
	/// Replaces the internal facade so any previously-loaded wallet state is reset.
	/// </summary>
	public void UseTestnet()
	{
		SetFacade(new WalletFacade(GalaChainNetworkConfig.Testnet()));
	}

	/// <summary>
	/// Switches the wallet to GalaChain mainnet. Mainnet is the default — you only
	/// need to call this if you previously switched to testnet or a custom network.
	/// </summary>
	public void UseMainnet()
	{
		SetFacade(new WalletFacade(GalaChainNetworkConfig.Mainnet()));
	}

	/// <summary>
	/// Switches the wallet to a custom GalaChain gateway. Call this in _Ready()
	/// of your main scene BEFORE any wallet operations.
	/// </summary>
	public void UseCustomNetwork(string apiBaseUrl, string channel = "asset", string contract = "token-contract")
	{
		var config = new GalaChainNetworkConfig
		{
			ApiBaseUrl = apiBaseUrl,
			Channel = channel,
			Contract = contract
		};
		SetFacade(new WalletFacade(config));
	}

	public void OpenWallet(Control parent)
	{
		_facade.OpenWallet(parent);
	}

	public void CloseWallet()
	{
		_facade.CloseWallet();
	}

	public bool HasWallet()
	{
		return _facade.HasWallet();
	}

	public bool IsUnlocked()
	{
		return _facade.IsUnlocked();
	}

	public string GetCurrentAddress()
	{
		return _facade.GetCurrentAddress();
	}

	public void RequestTransfer(string toAddress, string quantity, string tokenSymbol)
	{
		_facade.RequestTransfer(toAddress, quantity, tokenSymbol);
	}

	public void RequestBurn(string quantity, string tokenSymbol)
	{
		_facade.RequestBurn(quantity, tokenSymbol);
	}

	/// <summary>Allowance type code for a transfer allowance (granter's tokens can be moved by the spender).</summary>
	public const int ALLOWANCE_TYPE_TRANSFER = (int)AllowanceType.Transfer;
	/// <summary>Allowance type code for a burn allowance (granter's tokens can be burned by the spender).</summary>
	public const int ALLOWANCE_TYPE_BURN = (int)AllowanceType.Burn;

	/// <summary>
	/// Asks the player to grant a Transfer or Burn allowance to the given spender.
	/// Pass ALLOWANCE_TYPE_TRANSFER or ALLOWANCE_TYPE_BURN for allowanceType.
	/// expiresInDays = 0 means the allowance never expires (subject to the spender's uses limit).
	/// Listen for the AllowanceGranted or AllowanceGrantFailed signal.
	/// </summary>
	public void RequestGrantAllowance(string spender, string quantity, string tokenSymbol, int allowanceType, int expiresInDays = 0)
	{
		_facade.RequestGrantAllowance(spender, quantity, tokenSymbol, (AllowanceType)allowanceType, expiresInDays);
	}

	public void RequestSignMessage(string message)
	{
		_facade.RequestSignMessage(message);
	}

	/// <summary>
	/// Fetches latest balances from GalaChain. Fire-and-forget for GDScript —
	/// subscribe to the BalancesRefreshed signal to react when the refresh completes.
	/// Useful after a backend mint or reward grant to pick up new tokens.
	/// </summary>
	public void RefreshBalances()
	{
		_ = _facade.RefreshBalancesAsync();
	}

	/// <summary>
	/// Returns balances as an Array of Dictionaries for GDScript compatibility.
	/// Each dictionary has: symbol, display_amount, available_amount, collection,
	/// category, type, additional_key, instance.
	/// </summary>
	public Array<Dictionary<string, Variant>> GetBalances()
	{
		var result = new Array<Dictionary<string, Variant>>();
		foreach (var b in _facade.GetBalances())
		{
			result.Add(new Dictionary<string, Variant>
			{
				{ "symbol", b.Symbol },
				{ "display_amount", b.DisplayAmount },
				{ "available_amount", (double)b.AvailableAmount },
				{ "collection", b.Collection },
				{ "category", b.Category },
				{ "type", b.Type },
				{ "additional_key", b.AdditionalKey },
				{ "instance", b.Instance },
				{ "image_url", b.ImageUrl }
			});
		}
		return result;
	}

	/// <summary>
	/// Returns the GALA balance as a formatted string, or "" if not available.
	/// Convenience method for the most common balance query.
	/// </summary>
	public string GetGalaBalance()
	{
		foreach (var b in _facade.GetBalances())
		{
			if (b.Symbol.Equals("GALA", System.StringComparison.OrdinalIgnoreCase))
				return b.AvailableAmount.ToString("0.########");
		}
		return "";
	}
}
