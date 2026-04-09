# GDScript Compatibility — Implementation Notes

This document describes how the GalaChain Wallet plugin supports GDScript games while keeping all internals in C#.

## Status: Implemented

## Goal

A GDScript developer should be able to:
1. Enable C# in their Godot project (required for the NuGet crypto dependencies)
2. Copy the `addons/galachain_wallet/` folder into their project
3. Add the NuGet packages to the auto-generated `.csproj`
4. Use the wallet entirely from GDScript — no C# game code needed

## Approach: GDScript Bridge Node

Create a new `WalletBridge` node (extending `Node`) that wraps `WalletFacade` and exposes everything through Godot-native APIs: Godot signals instead of C# events, `Godot.Collections.Array` instead of `List<T>`, `Godot.Collections.Dictionary` instead of custom model classes.

This bridge is the **only file that needs to change** — all internal wallet code stays as-is. C# games continue using `WalletFacade` directly. GDScript games use `WalletBridge`.

### Architecture

```
GDScript Game Code
    |
    v
WalletBridge (Node, GDScript-compatible)    <-- NEW, thin wrapper
    |
    v
WalletFacade (C# class, unchanged)
    |
    v
[WalletService, GalaChainWallet, etc.]
```

## WalletBridge Implementation

### File: `addons/galachain_wallet/Scripts/Core/WalletBridge.cs`

```csharp
using Godot;
using Godot.Collections;
using GalaWallet.Models;

namespace GalaWallet.Core;

public partial class WalletBridge : Node
{
    private WalletFacade _facade = null!;

    // Godot signals — GDScript connects to these
    [Signal] public delegate void WalletCreatedEventHandler(string address);
    [Signal] public delegate void WalletImportedEventHandler(string address);
    [Signal] public delegate void WalletUnlockedEventHandler(string address);
    [Signal] public delegate void WalletLockedEventHandler();
    [Signal] public delegate void TransferCompletedEventHandler(string toAddress, string quantity, string symbol);
    [Signal] public delegate void TransferFailedEventHandler(string error);
    [Signal] public delegate void BalancesRefreshedEventHandler();

    public override void _Ready()
    {
        _facade = new WalletFacade();

        // Forward C# events to Godot signals
        _facade.WalletCreated += addr => EmitSignal(SignalName.WalletCreated, addr);
        _facade.WalletImported += addr => EmitSignal(SignalName.WalletImported, addr);
        _facade.WalletUnlocked += addr => EmitSignal(SignalName.WalletUnlocked, addr);
        _facade.WalletLocked += () => EmitSignal(SignalName.WalletLocked);
        _facade.TransferCompleted += (to, qty, sym) => EmitSignal(SignalName.TransferCompleted, to, qty, sym);
        _facade.TransferFailed += err => EmitSignal(SignalName.TransferFailed, err);
        _facade.BalancesRefreshed += () => EmitSignal(SignalName.BalancesRefreshed);
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

    // Returns balances as an Array of Dictionaries (GDScript-compatible)
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
                { "instance", b.Instance }
            });
        }
        return result;
    }
}
```

### Key Design Decisions

**Why a separate bridge instead of modifying WalletFacade?**
- `WalletFacade` is a plain C# class (no Godot base). Making it extend `Node` would change how C# games use it (they'd need to add it to the scene tree). The bridge keeps both paths clean.
- C# events are more ergonomic for C# code. Godot signals are more ergonomic for GDScript. One wraps the other.

**Why `Array<Dictionary>` instead of a custom Resource?**
- Dictionaries are immediately usable in GDScript without any C# knowledge.
- Custom `Resource` subclasses would be cleaner but require the GDScript developer to know about C# types.
- If needed later, we could create a `BalanceResource` that extends `Resource` for stronger typing.

**Why `decimal` becomes `double` in the bridge?**
- GDScript doesn't have `decimal`. Godot's `Variant` supports `float` (which is `double` in C#). For display purposes this is fine. The actual transfer quantities are passed as strings, which preserves precision.

## GDScript Usage

### Setup

1. Enable C# in the Godot project (Project > Project Settings > search "mono")
2. Copy `addons/galachain_wallet/` into the project
3. Add NuGet packages to the `.csproj`:
```xml
<PackageReference Include="Nethereum.Accounts" Version="5.8.0" />
<PackageReference Include="Nethereum.HDWallet" Version="5.8.0" />
<PackageReference Include="Nethereum.Signer" Version="5.8.0" />
```
4. Build the C# project once (Build > Build Project)
5. Add a `WalletBridge` node to the scene tree (or instantiate via script)

### Scene Tree

```
MyGame (Node2D)
  ├── WalletBridge          <-- add this node
  ├── WalletMount (Control) <-- for the wallet UI
  └── ... game nodes ...
```

### Example: GDScript Game

```gdscript
extends Control

@onready var wallet = $WalletBridge
@onready var wallet_mount = $WalletMount
@onready var balance_label = $BalanceLabel

func _ready():
    # Connect to wallet signals
    wallet.WalletUnlocked.connect(_on_wallet_unlocked)
    wallet.WalletLocked.connect(_on_wallet_locked)
    wallet.BalancesRefreshed.connect(_on_balances_refreshed)
    wallet.TransferCompleted.connect(_on_transfer_completed)
    wallet.TransferFailed.connect(_on_transfer_failed)

func _on_open_wallet_pressed():
    wallet.OpenWallet(wallet_mount)

func _on_close_wallet_pressed():
    wallet.CloseWallet()

func _on_buy_item_pressed():
    wallet.OpenWallet(wallet_mount)
    wallet.RequestTransfer("client|YOUR_GAME_WALLET", "100", "GALA")

func _on_wallet_unlocked(address: String):
    print("Wallet unlocked: ", address)

func _on_wallet_locked():
    balance_label.text = ""

func _on_balances_refreshed():
    var balances = wallet.GetBalances()
    for b in balances:
        if b["symbol"] == "GALA":
            balance_label.text = "GALA: %s" % b["display_amount"]

func _on_transfer_completed(to: String, quantity: String, symbol: String):
    print("Sent %s %s to %s" % [quantity, symbol, to])
    # Grant the purchased item here

func _on_transfer_failed(error: String):
    print("Transfer failed: ", error)
```

## What Changes vs Current Plugin

| Component | Change needed |
|-----------|--------------|
| `WalletBridge.cs` | **New file** — thin wrapper |
| `WalletFacade` | No changes |
| `WalletService` | No changes |
| `GalaChainWallet` (UI) | No changes |
| All other internals | No changes |
| `plugin.cfg` | No changes (bridge is a regular node, not an autoload) |
| `INTEGRATION_GUIDE.md` | Add GDScript section |

## What GDScript Games Still Need

1. **C# must be enabled** in the Godot project. This is a hard requirement — the crypto libraries (Nethereum, BouncyCastle) are .NET NuGet packages.
2. **NuGet packages** must be added to the `.csproj`. The GDScript developer needs to do this once (or we provide a setup script).
3. **Build the C# project** at least once before running, so the wallet assembly is compiled.

## Autoload Singleton

`WalletPlugin._EnterTree()` registers `WalletBridge` as an autoload singleton named "Wallet". When the plugin is enabled, `Wallet` is globally available in GDScript:

```gdscript
func _ready():
    Wallet.WalletUnlocked.connect(_on_wallet_unlocked)
    Wallet.BalancesRefreshed.connect(_on_balances_refreshed)

func _on_buy_pressed():
    Wallet.OpenWallet(wallet_mount)
    Wallet.RequestTransfer("client|...", "100", "GALA")
```

## Setup Script

The plugin includes `setup.sh` (bash) and `setup.ps1` (PowerShell) scripts that automate the NuGet dependency setup. Run from the game's project root:

```
# Windows
powershell -File addons/galachain_wallet/setup.ps1

# macOS/Linux
bash addons/galachain_wallet/setup.sh
```

The script:
1. Finds the `.csproj` in the current directory
2. Runs `dotnet add package` for each required NuGet package
3. Runs `dotnet restore`
4. Prints next steps (build in Godot, enable plugin)

If no `.csproj` exists, it prints instructions for enabling C# in the Godot project first.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Core/WalletBridge.cs` | GDScript-compatible Node wrapper |
| `WalletPlugin.cs` | Registers/removes the autoload singleton |
| `setup.sh` | Setup script for macOS/Linux |
| `setup.ps1` | Setup script for Windows |
