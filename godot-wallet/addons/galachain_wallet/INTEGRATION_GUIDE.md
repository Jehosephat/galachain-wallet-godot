# GalaChain Wallet Plugin — Integration Guide

This guide walks you through adding the GalaChain Wallet plugin to your Godot 4.x game. The plugin works with both **C#** and **GDScript** games.

## What This Plugin Does

The GalaChain Wallet is an in-game wallet module that lets players:

- Create a new wallet (generates a 12-word recovery phrase)
- Import a wallet from private key or recovery phrase
- View their GalaChain token balances
- Transfer tokens to other addresses with fee estimation

Your game code interacts with the wallet through a single class (`WalletFacade`). The wallet handles all cryptography, signing, and GalaChain API communication internally. Your game never touches private keys.

## What This Plugin Does NOT Do

This plugin is strictly a **client-side player wallet**. It does not handle:

- **Minting tokens to players** — this requires a key with mint authority, which must never ship in client code. Mint from your game backend and call `RefreshBalancesAsync()` on the client afterward to show the new tokens.
- **Burning tokens on players' behalf** — same reason; authority keys belong on a backend.
- **Granting rewards or airdrops** — do this server-side.
- **Any operation signed with a key other than the player's wallet key** — if your game backend needs to sign GalaChain transactions, it should use its own stack, not this plugin.

The rule of thumb: if the operation requires player approval, it belongs in this plugin. If it requires game authority, it belongs on your backend.

## Prerequisites

- Godot 4.5+ with C#/.NET support enabled
- .NET 8.0 SDK
- Your project must have a `.csproj` file (created automatically when you enable C# in Godot)

## Step 1: Install the Plugin

Copy the `addons/galachain_wallet/` folder into your project's `addons/` directory:

```
your_game/
  addons/
    galachain_wallet/    <-- copy this entire folder
      plugin.cfg
      WalletPlugin.cs
      scenes/
      Scripts/
  project.godot
  YourGame.csproj
```

## Step 2: Add NuGet Dependencies

Add these packages to your game's `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Nethereum.Accounts" Version="5.8.0" />
  <PackageReference Include="Nethereum.HDWallet" Version="5.8.0" />
  <PackageReference Include="Nethereum.Signer" Version="5.8.0" />
</ItemGroup>
```

Then restore packages:

```
dotnet restore
```

## Step 3: Enable the Plugin

In Godot: **Project > Project Settings > Plugins** — find "GalaChain Wallet" and set it to **Active**.

Build the project once (**Build > Build Project** or `dotnet build`) so Godot picks up the C# types.

## Step 4: Add a Wallet Mount to Your Scene

The wallet UI needs a parent `Control` node to attach to. Add an empty `Control` node to your game scene where you want the wallet to appear:

1. Add a `Control` node to your scene (name it something like `WalletMount`)
2. Set its anchors to fill the area where the wallet should render (e.g., right half of the screen)
3. Set `Mouse > Filter` to **Ignore** so it doesn't block clicks on game UI underneath

The wallet will instantiate itself as a child of this node when opened.

## Step 5: Use WalletFacade in Your Game Code

`WalletFacade` is the only class your game code needs to interact with.

### Minimal Setup

```csharp
using Godot;
using GalaWallet.Core;

public partial class MyGame : Control
{
    private WalletFacade _wallet = null!;
    private Control _walletMount = null!;

    public override void _Ready()
    {
        _wallet = new WalletFacade();
        _walletMount = GetNode<Control>("%WalletMount");
    }
}
```

### Opening and Closing the Wallet

```csharp
// Show the wallet UI (creates it on first call, shows it on subsequent calls)
_wallet.OpenWallet(_walletMount);

// Hide the wallet UI
_wallet.CloseWallet();
```

### Checking Wallet State

```csharp
// Does the player have a wallet created/imported?
bool hasWallet = _wallet.HasWallet();

// Is the wallet currently unlocked (keys in memory)?
bool unlocked = _wallet.IsUnlocked();

// Get the player's Ethereum-style address (0x-prefixed)
string address = _wallet.GetCurrentAddress();
```

### Requesting a Transfer from Game Code

This is the most common integration point. Your game requests a transfer, and the wallet handles confirmation, fee display, signing, and submission.

```csharp
// Open wallet and request a transfer
_wallet.OpenWallet(_walletMount);
_wallet.RequestTransfer(
    "client|5f58d8641586e117c5e68834",  // recipient address
    "15",                                 // quantity as string
    "GALA"                                // token symbol
);
```

**What happens:**
1. If the wallet is locked, the unlock dialog pops up automatically
2. After unlock, balances are fetched and the transfer dialog opens pre-filled
3. A dry-run simulates the transaction and shows the estimated fee
4. The player reviews and clicks OK to confirm
5. The wallet signs the transaction and submits it to GalaChain
6. Balances refresh automatically after a successful transfer

**Address formats:**
- `eth|<hex_address_without_0x>` — for Ethereum-style addresses
- `client|<gala_user_id>` — for GalaChain client addresses

### Refreshing Balances

```csharp
await _wallet.RefreshBalancesAsync();
```

### Reading Balances

```csharp
var balances = _wallet.GetBalances();
foreach (var b in balances)
{
    GD.Print($"{b.Symbol}: {b.AvailableAmount}");
}
```

### Subscribing to Wallet Events

The wallet fires C# events on `WalletFacade` that your game can subscribe to:

```csharp
_wallet.WalletCreated += (address) =>
{
    GD.Print($"Wallet created: {address}");
};

_wallet.WalletUnlocked += (address) =>
{
    GD.Print($"Wallet unlocked: {address}");
};

_wallet.WalletLocked += () =>
{
    GD.Print("Wallet locked");
};

_wallet.BalancesRefreshed += () =>
{
    // Update your game's balance display
    var balances = _wallet.GetBalances();
};

_wallet.TransferCompleted += (to, quantity, symbol) =>
{
    GD.Print($"Sent {quantity} {symbol} to {to}");
    // Grant item, update inventory, etc.
};

_wallet.TransferFailed += (error) =>
{
    GD.Print($"Transfer failed: {error}");
};
```

**Available events:**

| Event | Args | Fired when |
|-------|------|-----------|
| `WalletCreated` | `string address` | After wallet creation |
| `WalletImported` | `string address` | After private key or mnemonic import |
| `WalletUnlocked` | `string address` | After successful unlock |
| `WalletLocked` | (none) | After manual lock or auto-lock timeout |
| `TransferCompleted` | `string to, string qty, string symbol` | After successful transfer |
| `TransferFailed` | `string error` | After failed transfer |
| `BalancesRefreshed` | (none) | After balances are updated (any trigger) |

## Complete Example

```csharp
using Godot;
using GalaWallet.Core;

public partial class GameShop : Control
{
    private WalletFacade _wallet = null!;
    private Control _walletMount = null!;
    private Label _balanceLabel = null!;

    public override void _Ready()
    {
        _wallet = new WalletFacade();
        _walletMount = GetNode<Control>("%WalletMount");
        _balanceLabel = GetNode<Label>("%BalanceLabel");

        GetNode<Button>("%OpenWalletButton").Pressed += () =>
        {
            _wallet.OpenWallet(_walletMount);
        };

        GetNode<Button>("%BuySwordButton").Pressed += () =>
        {
            _wallet.OpenWallet(_walletMount);
            _wallet.RequestTransfer(
                "client|YOUR_GAME_WALLET_ID",
                "100",
                "GALA"
            );
        };

        // React to wallet events
        _wallet.BalancesRefreshed += UpdateBalanceDisplay;
        _wallet.WalletLocked += () => _balanceLabel.Text = "";
        _wallet.TransferCompleted += (to, qty, sym) =>
        {
            GD.Print($"Purchase complete: {qty} {sym}");
            // Grant the sword to the player here
        };
    }

    private void UpdateBalanceDisplay()
    {
        var balances = _wallet.GetBalances();
        if (balances.Count > 0)
            _balanceLabel.Text = $"GALA: {balances[0].AvailableAmount:0.##}";
    }
}
```

## How the Wallet Works (For Context)

### Wallet Lifecycle

1. **Create**: Generates a 12-word BIP39 mnemonic, derives an Ethereum-style keypair (secp256k1), encrypts with the player's password (AES-256-GCM + PBKDF2), saves to disk.
2. **Import**: Accepts a private key (hex) or recovery phrase. Encrypts and saves.
3. **Unlock**: Player enters password. The encrypted keystore is decrypted and keys are held in memory.
4. **Lock**: Keys are cleared from memory. Happens manually or automatically after 5 minutes of inactivity.

### Where Keys Are Stored

The encrypted wallet file is stored at `user://wallet/wallet.json` using Godot's application data path. On Windows this is typically `%APPDATA%/Godot/app_userdata/YourProject/wallet/wallet.json`.

The file contains AES-256-GCM encrypted key material. The plaintext private key or mnemonic is never written to disk.

### Transfer Security Model

- Transactions are signed locally using the player's private key (secp256k1 + keccak256)
- Every transaction gets a unique key and a 3-minute expiration (`dtoExpiresAt`)
- Signatures are verified locally before submission (recovered address must match wallet)
- A dry-run simulates the transaction on GalaChain before the player confirms
- The wallet only supports `TransferToken` — it refuses to sign anything else
- Your game code cannot access the private key or mnemonic

### Network Configuration

The wallet connects to GalaChain mainnet by default:
- Gateway: `https://gateway-mainnet.galachain.com/api`
- Channel: `asset`
- Contract: `token-contract`

#### Switching to Testnet (C#)

Pass a testnet config when creating the facade:

```csharp
// Shortcut — built-in testnet gateway URL
_wallet = new WalletFacade(GalaChainNetworkConfig.Testnet());

// Or build a custom config explicitly
var config = new GalaChainNetworkConfig
{
    ApiBaseUrl = "https://galachain-gateway-chain-platform-stage-chain-platform-eks.stage.galachain.com/api",
    Channel = "asset",
    Contract = "token-contract"
};
_wallet = new WalletFacade(config);
```

Do this in `_Ready()` before any wallet operations. Switching networks is not something the end user toggles — it's a build-time decision made by the game developer.

#### Switching to Testnet (GDScript)

The `Wallet` autoload starts on mainnet by default. Call `UseTestnet()` once in your main scene's `_ready()`, before any other wallet operations:

```gdscript
func _ready():
    Wallet.UseTestnet()

    # Now connect signals, open wallet, etc. as usual
    Wallet.WalletUnlocked.connect(_on_wallet_unlocked)
    Wallet.OpenWallet($WalletMount)
```

Other options:

```gdscript
Wallet.UseMainnet()                                    # explicit mainnet (default)
Wallet.UseCustomNetwork("https://my-gateway/api")      # custom gateway
Wallet.UseCustomNetwork("https://my-gateway/api", "my-channel", "my-contract")
```

**Important**: Call the network-switching method before any wallet operations. It replaces the internal wallet state — calling it after a wallet has been created, unlocked, or had transactions will reset everything.

## API Reference

### WalletFacade

| Method | Returns | Description |
|--------|---------|-------------|
| `OpenWallet(Control parent)` | `void` | Shows the wallet UI. Creates it on first call. |
| `CloseWallet()` | `void` | Hides the wallet UI. |
| `HasWallet()` | `bool` | Whether a wallet exists (created or imported). |
| `IsUnlocked()` | `bool` | Whether the wallet is currently unlocked. |
| `GetCurrentAddress()` | `string` | The wallet's 0x-prefixed Ethereum address. |
| `GetBalances()` | `List<TokenBalanceModel>` | Returns the current in-memory balance list. |
| `RefreshBalancesAsync()` | `Task` | Fetches latest balances from GalaChain. |
| `RequestTransfer(to, quantity, symbol)` | `void` | Opens a pre-filled transfer dialog. Auto-prompts unlock if needed. |
| `RequestBurn(quantity, symbol)` | `void` | Opens a pre-filled burn confirmation dialog. Auto-prompts unlock if needed. |
| `RequestGrantAllowance(spender, quantity, symbol, type, expiresInDays)` | `void` | Opens a pre-filled allowance grant dialog. `type` is `AllowanceType.Transfer` or `AllowanceType.Burn`; `expiresInDays = 0` means never expires. |
| `RequestSignMessage(message)` | `void` | Opens an EIP-191 sign confirmation dialog. Used for wallet login. |

### GalaChainNetworkConfig

| Property | Default | Description |
|----------|---------|-------------|
| `ApiBaseUrl` | `https://gateway-mainnet.galachain.com/api` | Gateway API base URL |
| `Channel` | `asset` | GalaChain channel name |
| `Contract` | `token-contract` | Contract name |
| `ReadTimeoutSeconds` | `15` | Timeout for balance/dry-run requests |
| `WriteTimeoutSeconds` | `30` | Timeout for transfer submission |

---

## GDScript Integration

The plugin includes a `WalletBridge` node that exposes all wallet functionality through Godot signals and GDScript-compatible types. When the plugin is enabled, a `Wallet` autoload singleton is registered automatically.

### Step 1: Enable C# in Your Godot Project

If your project is GDScript-only, you need to enable C# support first. The wallet plugin uses .NET crypto libraries that require the C# build pipeline — but your game code stays 100% GDScript.

1. Make sure you have the **.NET version of Godot** (not the standard version). Download it from [godotengine.org](https://godotengine.org/download) — it's labeled "Godot Engine - .NET". If you've been using the standard version, you can switch safely — the .NET editor opens existing GDScript projects without any changes to your scenes, scripts, or project settings. It's the same editor with added C# support.
2. Make sure the **.NET 8.0 SDK** (or later) is installed. Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
3. Open your project in the .NET version of Godot
4. Create a C# solution: **Project > Tools > C# > Create C# Solution**. This generates a `.csproj` and `.sln` file in your project root. You won't need to edit these files directly.
5. Build once: **MSBuild > Build > Build Project** (or the build button in the top-right toolbar). This verifies the C# pipeline works.

After this, you never need to write C# — the wallet plugin's C# code compiles automatically as part of your project.

### Step 2: Install the Plugin

1. Copy the `addons/galachain_wallet/` folder into your project's `addons/` directory
2. From your project root (where `project.godot` lives), run the setup script:
   - **Windows**: `powershell -File addons/galachain_wallet/setup.ps1`
   - **macOS/Linux**: `bash addons/galachain_wallet/setup.sh`

   This adds the required NuGet packages (Nethereum crypto libraries) to your `.csproj` and runs `dotnet restore`. If you prefer to do it manually, add these to your `.csproj`:
   ```xml
   <PackageReference Include="Nethereum.Accounts" Version="5.8.0" />
   <PackageReference Include="Nethereum.HDWallet" Version="5.8.0" />
   <PackageReference Include="Nethereum.Signer" Version="5.8.0" />
   ```

3. Back in Godot, build the C# project again (**Build > Build Project**)
4. Enable the plugin: **Project > Project Settings > Plugins > GalaChain Wallet**
5. The `Wallet` autoload singleton is now available globally in GDScript

### Scene Setup

Add a `Control` node to your scene for the wallet UI to mount to:

```
MyGame (Node2D or Control)
  └── WalletMount (Control)   <-- set anchors/size for where wallet appears
```

Set `WalletMount`'s Mouse Filter to **Ignore** so it doesn't block clicks on your game UI.

### GDScript Example

```gdscript
extends Control

@onready var wallet_mount = $WalletMount
@onready var balance_label = $BalanceLabel

func _ready():
    Wallet.WalletUnlocked.connect(_on_wallet_unlocked)
    Wallet.WalletLocked.connect(_on_wallet_locked)
    Wallet.BalancesRefreshed.connect(_on_balances_refreshed)
    Wallet.TransferCompleted.connect(_on_transfer_completed)

func _on_open_wallet_pressed():
    Wallet.OpenWallet(wallet_mount)

func _on_buy_item_pressed():
    Wallet.OpenWallet(wallet_mount)
    Wallet.RequestTransfer("client|YOUR_GAME_WALLET", "100", "GALA")

func _on_wallet_unlocked(address: String):
    print("Unlocked: ", address)

func _on_wallet_locked():
    balance_label.text = ""

func _on_balances_refreshed():
    balance_label.text = "GALA: " + Wallet.GetGalaBalance()

func _on_transfer_completed(to: String, quantity: String, symbol: String):
    print("Sent %s %s to %s" % [quantity, symbol, to])
```

### GDScript API Reference

**Methods** (on the `Wallet` autoload):

| Method | Returns | Description |
|--------|---------|-------------|
| `OpenWallet(parent: Control)` | `void` | Shows the wallet UI |
| `CloseWallet()` | `void` | Hides the wallet UI |
| `HasWallet()` | `bool` | Whether a wallet exists |
| `IsUnlocked()` | `bool` | Whether the wallet is unlocked |
| `GetCurrentAddress()` | `String` | The wallet's `eth\|` address |
| `RequestTransfer(to, qty, symbol)` | `void` | Opens pre-filled transfer dialog |
| `RequestBurn(qty, symbol)` | `void` | Opens pre-filled burn dialog |
| `RequestGrantAllowance(spender, qty, symbol, allowanceType, expiresInDays)` | `void` | Opens pre-filled allowance grant dialog. Use `Wallet.ALLOWANCE_TYPE_TRANSFER` or `Wallet.ALLOWANCE_TYPE_BURN` for `allowanceType`. `expiresInDays = 0` means never expires. |
| `RequestSignMessage(message)` | `void` | Opens EIP-191 sign dialog for wallet login |
| `RefreshBalances()` | `void` | Fetches latest balances. Fire-and-forget — listen for `BalancesRefreshed`. |
| `GetBalances()` | `Array[Dictionary]` | All token balances |
| `GetGalaBalance()` | `String` | GALA balance as a formatted string |

**Signals** (on the `Wallet` autoload):

| Signal | Args | Fired when |
|--------|------|-----------|
| `WalletCreated` | `address: String` | After wallet creation |
| `WalletImported` | `address: String` | After import (key or mnemonic) |
| `WalletUnlocked` | `address: String` | After successful unlock |
| `WalletLocked` | (none) | After lock or auto-lock |
| `TransferCompleted` | `to: String, qty: String, symbol: String` | After successful transfer |
| `TransferFailed` | `error: String` | After failed transfer |
| `BurnCompleted` | `qty: String, symbol: String` | After successful burn |
| `BurnFailed` | `error: String` | After failed burn |
| `AllowanceGranted` | `spender: String, qty: String, symbol: String, allowanceType: String` | After successful allowance grant. `allowanceType` is `"Transfer"` or `"Burn"`. |
| `AllowanceGrantFailed` | `error: String` | After failed allowance grant |
| `MessageSigned` | `message: String, signature: String, address: String` | After the player signs a message |
| `MessageSignDeclined` | (none) | Player cancelled the sign dialog |
| `BalancesRefreshed` | (none) | After balances update |

**Balance Dictionary** (returned by `GetBalances()`):

| Key | Type | Description |
|-----|------|-------------|
| `symbol` | `String` | Token display name (e.g., "GALA") |
| `display_amount` | `String` | Formatted amount with lock info |
| `available_amount` | `float` | Available balance as a number |
| `collection` | `String` | GalaChain token collection |
| `category` | `String` | Token category |
| `type` | `String` | Token type |
| `additional_key` | `String` | Additional key |
| `instance` | `String` | Token instance ID |

## Troubleshooting

**"Unable to load addon script from path: WalletPlugin.cs"** — Godot cannot find the compiled C# assemblies when it tries to enable the plugin. This usually means the C# project was not built before the plugin was activated, or the build output isn't where Godot expects it. To fix:

1. Disable the plugin in **Project > Project Settings > Plugins** (it may have auto-disabled itself already).
2. Ensure you have a `.csproj` file in your project root. If not, create the C# solution first: **Project > Tools > C# > Create C# Solution**.
3. Add the required NuGet packages if you haven't already (see Step 2 above), then run `dotnet restore`.
4. Build inside the Godot editor: click **Build > Build Project** (or the hammer icon in the top-right toolbar). A command-line `dotnet build` puts output in a different location than Godot expects — always build from the editor.
5. Restart the Godot editor (close and reopen the project). This forces a fresh load of the compiled assemblies.
6. Now enable the plugin: **Project > Project Settings > Plugins > GalaChain Wallet > Active**.

The key insight is that the build must happen *before* enabling the plugin, and building from within the Godot editor ensures the DLL lands in `.godot/mono/temp/bin/Debug/` where the editor looks for it.

**"Wallet service is not initialized"** — `WalletFacade` was not created before calling wallet methods. Make sure you create it in `_Ready()`.

**"No balance found for token X"** — Balances haven't been fetched yet. The wallet fetches balances automatically on unlock, but if you call `RequestTransfer` before the wallet has been opened/unlocked at least once, there are no balances to search. Open the wallet first.

**Transfer dialog shows "Estimated fee: unavailable (network error)"** — The GalaChain gateway is unreachable. Check your network connection. The wallet uses a 15-second timeout for fee estimation.

**Wallet auto-locks unexpectedly** — The wallet locks after 5 minutes of inactivity. Any wallet action (transfer, balance refresh, copy address) resets the timer.
