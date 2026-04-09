# GalaChain Wallet Plugin — Integration Guide

This guide walks you through adding the GalaChain Wallet plugin to your Godot 4.x C# game.

## What This Plugin Does

The GalaChain Wallet is an in-game wallet module that lets players:

- Create a new wallet (generates a 12-word recovery phrase)
- Import a wallet from private key or recovery phrase
- View their GalaChain token balances
- Transfer tokens to other addresses with fee estimation

Your game code interacts with the wallet through a single class (`WalletFacade`). The wallet handles all cryptography, signing, and GalaChain API communication internally. Your game never touches private keys.

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

## Complete Example

```csharp
using Godot;
using GalaWallet.Core;

public partial class GameShop : Control
{
    private WalletFacade _wallet = null!;
    private Control _walletMount = null!;

    public override void _Ready()
    {
        _wallet = new WalletFacade();
        _walletMount = GetNode<Control>("%WalletMount");

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

To use a different environment (e.g., testnet), pass a custom config when creating the service:

```csharp
var config = new GalaChainNetworkConfig
{
    ApiBaseUrl = "https://your-gateway-url/api",
    Channel = "asset",
    Contract = "token-contract"
};

var galaChainClient = new GalaChainClient(config);
var transferClient = new GalaTransferClient(config);

var walletService = new WalletService(
    galaChainClient: galaChainClient,
    galaTransferClient: transferClient
);

var wallet = new WalletFacade(walletService);
```

## API Reference

### WalletFacade

| Method | Returns | Description |
|--------|---------|-------------|
| `OpenWallet(Control parent)` | `void` | Shows the wallet UI. Creates it on first call. |
| `CloseWallet()` | `void` | Hides the wallet UI. |
| `HasWallet()` | `bool` | Whether a wallet exists (created or imported). |
| `IsUnlocked()` | `bool` | Whether the wallet is currently unlocked. |
| `GetCurrentAddress()` | `string` | The wallet's 0x-prefixed Ethereum address. |
| `RefreshBalancesAsync()` | `Task` | Fetches latest balances from GalaChain. |
| `RequestTransfer(to, quantity, symbol)` | `void` | Opens a pre-filled transfer dialog. Auto-prompts unlock if needed. |

### GalaChainNetworkConfig

| Property | Default | Description |
|----------|---------|-------------|
| `ApiBaseUrl` | `https://gateway-mainnet.galachain.com/api` | Gateway API base URL |
| `Channel` | `asset` | GalaChain channel name |
| `Contract` | `token-contract` | Contract name |
| `ReadTimeoutSeconds` | `15` | Timeout for balance/dry-run requests |
| `WriteTimeoutSeconds` | `30` | Timeout for transfer submission |

## Troubleshooting

**"Wallet service is not initialized"** — `WalletFacade` was not created before calling wallet methods. Make sure you create it in `_Ready()`.

**"No balance found for token X"** — Balances haven't been fetched yet. The wallet fetches balances automatically on unlock, but if you call `RequestTransfer` before the wallet has been opened/unlocked at least once, there are no balances to search. Open the wallet first.

**Transfer dialog shows "Estimated fee: unavailable (network error)"** — The GalaChain gateway is unreachable. Check your network connection. The wallet uses a 15-second timeout for fee estimation.

**Wallet auto-locks unexpectedly** — The wallet locks after 5 minutes of inactivity. Any wallet action (transfer, balance refresh, copy address) resets the timer.
