using Godot;
using Nethereum.Signer;
using GalaWallet.Core;
using GalaWallet.Models;

// =====================================================================================
// TESTING NOTE — demo backend secrets
// =====================================================================================
// The Grant Allowance / Spend Allowance buttons require a backend signing identity:
//   - _demoBackendPrivateKey — hex secp256k1 key for the backend
//   - _demoBackendIdentity   — the GalaChain alias (eth|... or client|...) that key
//                              corresponds to on chain
//
// These are intentionally EMPTY in the committed file. Before testing the allowance
// flow, fill them in via a local partial file: create `WalletDemoGame.LocalSecrets.cs`
// next to this file and implement the partial method `LoadLocalSecrets()`. See
// `WalletDemoGame.LocalSecrets.cs.example` for the pattern. That file name is listed
// in .gitignore so the real secrets don't get committed.
//
// DO NOT paste keys directly into this file — it's tracked in git.
// =====================================================================================

public partial class WalletDemoGame : Control
{
	private WalletFacade _walletFacade = null!;
	private GalaChainNetworkConfig _networkConfig = GalaChainNetworkConfig.Mainnet();
	private OptionButton _networkOption = null!;
	private Button _openWalletButton = null!;
	private Button _closeWalletButton = null!;
	private Button _makePurchaseButton = null!;
	private Button _burnGalaButton = null!;
	private Button _loginButton = null!;
	private Button _grantAllowanceButton = null!;
	private Button _spendAllowanceButton = null!;
	private Control _walletMount = null!;
	private Label _gameStatusLabel = null!;
	private Label _gameBalanceLabel = null!;

	// Stand-in "game backend" identity. In a real deployment this key would live
	// on your game server and would correspond to the GalaChain user identity
	// (e.g. client|5f58d8641586e117c5e68834) that the player granted allowance to.
	// Populate these in a local, untracked WalletDemoGame.LocalSecrets.cs — see the
	// banner comment above. Leaving them empty disables the allowance flow but the
	// rest of the demo still runs.
	private string _demoBackendPrivateKey = "";
	private string _demoBackendIdentity = "";

	// Implemented by WalletDemoGame.LocalSecrets.cs (untracked) when present.
	// If that file isn't on disk the partial method is compiled out — the fields
	// above stay empty and the allowance demo logs a reminder instead of signing.
	partial void LoadLocalSecrets();

	public override void _Ready()
	{
		_networkOption = GetNode<OptionButton>("%NetworkOption");
		_openWalletButton = GetNode<Button>("%OpenWalletButton");
		_closeWalletButton = GetNode<Button>("%CloseWalletButton");
		_makePurchaseButton = GetNode<Button>("%MakePurchaseButton");
		_burnGalaButton = GetNode<Button>("%BurnGalaButton");
		_loginButton = GetNode<Button>("%LoginButton");
		_grantAllowanceButton = GetNode<Button>("%GrantAllowanceButton");
		_spendAllowanceButton = GetNode<Button>("%SpendAllowanceButton");
		_walletMount = GetNode<Control>("%WalletMount");
		_gameStatusLabel = GetNode<Label>("%GameStatusLabel");
		_gameBalanceLabel = GetNode<Label>("%GameBalanceLabel");

		_networkOption.ItemSelected += OnNetworkChanged;
		_openWalletButton.Pressed += OnOpenWalletPressed;
		_closeWalletButton.Pressed += OnCloseWalletPressed;
		_makePurchaseButton.Pressed += OnMakePurchasePressed;
		_burnGalaButton.Pressed += OnBurnGalaPressed;
		_loginButton.Pressed += OnLoginPressed;
		_grantAllowanceButton.Pressed += OnGrantAllowancePressed;
		_spendAllowanceButton.Pressed += OnSpendAllowancePressed;

		LoadLocalSecrets();

		if (string.IsNullOrWhiteSpace(_demoBackendPrivateKey) || string.IsNullOrWhiteSpace(_demoBackendIdentity))
		{
			GD.Print("[Demo] Backend secrets not configured — Grant/Spend Allowance buttons will be disabled.");
			GD.Print("[Demo] See the banner comment in WalletDemoGame.cs for how to provide them.");
			_grantAllowanceButton.Disabled = true;
			_spendAllowanceButton.Disabled = true;
		}
		else
		{
			var backendKey = new EthECKey(_demoBackendPrivateKey);
			GD.Print($"[Demo] Backend signer address (derived from configured key): {backendKey.GetPublicAddress()}");
			GD.Print($"[Demo] Backend identity used for grants and spends: {_demoBackendIdentity}");
		}

		CreateWalletFacade(GalaChainNetworkConfig.Mainnet());
		UpdateStatus();
	}

	private void CreateWalletFacade(GalaChainNetworkConfig config)
	{
		_networkConfig = config;

		// Close any existing wallet UI so the old instance is released
		if (_walletFacade != null)
		{
			_walletFacade.CloseWallet();
			foreach (var child in _walletMount.GetChildren())
			{
				child.QueueFree();
			}
		}

		_walletFacade = new WalletFacade(config);

		_walletFacade.WalletCreated += _ => UpdateStatus();
		_walletFacade.WalletImported += _ => UpdateStatus();
		_walletFacade.WalletUnlocked += _ => UpdateStatus();
		_walletFacade.WalletLocked += UpdateStatus;
		_walletFacade.BalancesRefreshed += UpdateBalanceDisplay;
		_walletFacade.TransferCompleted += (to, qty, sym) =>
		{
			GD.Print($"[Demo] Transfer completed: {qty} {sym} to {to}");
		};
		_walletFacade.TransferFailed += err =>
		{
			GD.Print($"[Demo] Transfer failed: {err}");
		};
		_walletFacade.BurnCompleted += (qty, sym) =>
		{
			GD.Print($"[Demo] Burn completed: {qty} {sym}");
		};
		_walletFacade.BurnFailed += err =>
		{
			GD.Print($"[Demo] Burn failed: {err}");
		};
		_walletFacade.MessageSigned += (msg, sig, addr) =>
		{
			GD.Print($"[Demo] Login signature received");
			GD.Print($"  address:   {addr}");
			GD.Print($"  signature: {sig}");
			GD.Print("  (Send these to your game server, which verifies the signature recovers to the address.)");
		};
		_walletFacade.MessageSignDeclined += () =>
		{
			GD.Print("[Demo] Login declined by user.");
		};
		_walletFacade.AllowanceGranted += (spender, qty, sym, type) =>
		{
			GD.Print($"[Demo] {type} allowance granted: {qty} {sym} to {spender}");
		};
		_walletFacade.AllowanceGrantFailed += err =>
		{
			GD.Print($"[Demo] Allowance grant failed: {err}");
		};
	}

	private void OnNetworkChanged(long index)
	{
		var config = index == 1
			? GalaChainNetworkConfig.Testnet()
			: GalaChainNetworkConfig.Mainnet();

		string label = index == 1 ? "Testnet" : "Mainnet";
		GD.Print($"[Demo] Switching wallet to {label} ({config.ApiBaseUrl})");

		CreateWalletFacade(config);
		UpdateStatus();
	}

	private void OnOpenWalletPressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		UpdateStatus();
	}

	private void OnCloseWalletPressed()
	{
		_walletFacade.CloseWallet();
		UpdateStatus();
	}

	private void OnMakePurchasePressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		_walletFacade.RequestTransfer("client|5f58d8641586e117c5e68834", "15", "GALA");
		UpdateStatus();
	}

	private void OnBurnGalaPressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		_walletFacade.RequestBurn("5", "GALA");
		UpdateStatus();
	}

	private void OnLoginPressed()
	{
		_walletFacade.OpenWallet(_walletMount);

		// A real game server would generate this nonce and timestamp and give it
		// to the client. The server then verifies the returned signature recovers
		// to the expected address.
		string nonce = System.Guid.NewGuid().ToString("N").Substring(0, 16);
		string timestamp = System.DateTime.UtcNow.ToString("o");
		string message =
			"Sign in to WalletDemoGame\n" +
			$"Nonce: {nonce}\n" +
			$"Timestamp: {timestamp}";

		_walletFacade.RequestSignMessage(message);
		UpdateStatus();
	}

	private void OnGrantAllowancePressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		// Example: grant the game backend a 10 GALA transfer allowance that expires in 7 days.
		_walletFacade.RequestGrantAllowance(
			_demoBackendIdentity,
			"10",
			"GALA",
			AllowanceType.Transfer,
			expiresInDays: 7);
		UpdateStatus();
	}

	/// <summary>
	/// Demonstrates the "backend uses a granted allowance" flow. The demo app signs
	/// a TransferToken DTO with its OWN key (not the player's) — the player granted
	/// it a transfer allowance earlier, so no wallet prompt is needed. from = player,
	/// to = the backend's own identity.
	/// </summary>
	private async void OnSpendAllowancePressed()
	{
		if (!_walletFacade.HasWallet())
		{
			GD.Print("[Demo] No wallet to spend from. Create or unlock a wallet first.");
			return;
		}

		string playerEthAddress = _walletFacade.GetCurrentAddress();
		string playerAlias = ToGalaAlias(playerEthAddress);

		var request = new GalaTransferTokenRequest
		{
			from = playerAlias,
			to = _demoBackendIdentity,
			quantity = "5",
			tokenInstance = new GalaTokenInstance
			{
				collection = "GALA",
				category = "Unit",
				type = "none",
				additionalKey = "none",
				instance = "0"
			},
			uniqueKey = $"demo-backend-{System.Guid.NewGuid()}",
			dtoExpiresAt = System.DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds()
		};

		var signer = new GalaSigner();
		signer.SignTransfer(request, _demoBackendPrivateKey);

		GD.Print($"[Demo] Backend signing spend-allowance transfer: 5 GALA {playerAlias} -> {_demoBackendIdentity}");

		var transferClient = new GalaTransferClient(_networkConfig);
		var result = await transferClient.TransferAsync(request, _demoBackendIdentity);

		if (result.IsSuccess)
		{
			GD.Print("[Demo] Spend-allowance transfer submitted successfully.");
			await _walletFacade.RefreshBalancesAsync();
		}
		else
		{
			GD.Print($"[Demo] Spend-allowance transfer failed: {result.ErrorMessage}");
			GD.Print("[Demo] Note: this demo's backend key is generated randomly per session.");
			GD.Print($"[Demo] For on-chain success, the backend key must correspond to {_demoBackendIdentity}.");
		}
	}

	private static string ToGalaAlias(string ethAddress)
	{
		string trimmed = ethAddress.Trim();
		if (trimmed.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
			return $"eth|{trimmed[2..]}";
		return trimmed;
	}

	private void UpdateStatus()
	{
		if (!_walletFacade.HasWallet())
		{
			_gameStatusLabel.Text = "Game sees: no wallet";
			_gameBalanceLabel.Text = "";
			return;
		}

		_gameStatusLabel.Text = _walletFacade.IsUnlocked()
			? $"Game sees unlocked wallet: {_walletFacade.GetCurrentAddress()}"
			: $"Game sees locked wallet: {_walletFacade.GetCurrentAddress()}";

		UpdateBalanceDisplay();
	}

	private void UpdateBalanceDisplay()
	{
		var balances = _walletFacade.GetBalances();

		foreach (var b in balances)
		{
			if (string.Equals(b.Symbol, "GALA", System.StringComparison.OrdinalIgnoreCase))
			{
				_gameBalanceLabel.Text = $"GALA Balance: {b.AvailableAmount:0.########}";
				return;
			}
		}

		_gameBalanceLabel.Text = "";
	}
}
