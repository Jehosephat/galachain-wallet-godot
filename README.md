# GalaChain Wallet for Godot

An embedded in-game GalaChain wallet plugin for Godot 4.x games. Players can create/import wallets, view balances, and sign token transfers and burns — all from inside the game. Works with both **C#** and **GDScript** games.

## Features

- **Wallet lifecycle**: create, import (private key or recovery phrase), unlock, lock, auto-lock on idle
- **Encrypted local keystore** — AES-256-GCM with PBKDF2 key derivation
- **Balance display** with token icons and metadata from GalaChain
- **TransferToken** — player-signed transfers with dry-run fee estimation
- **BurnTokens** — player-initiated burns with dry-run fee estimation
- **Game-initiated flows** — game code can prompt the player for a transfer or burn via `RequestTransfer`/`RequestBurn`, with pre-filled confirmation dialogs
- **Wallet events** — C# events and Godot signals for wallet lifecycle and transaction outcomes
- **GDScript compatibility** — the plugin exposes a `Wallet` autoload singleton for GDScript games
- **Test coverage** — 32 unit tests including golden vector tests against real mainnet transactions

## Scope Boundary

This plugin is **strictly client-side**. It handles operations the player themselves signs with their own wallet key (transfers, burns). Operations that require game authority (minting, reward grants, burning on players' behalf) belong on a game backend server — not in this plugin.

See [AGENTS.md](AGENTS.md#scope-boundary) for the full scope discussion.

## Quick Start

1. Copy `addons/galachain_wallet/` into your Godot project
2. Run the setup script to install NuGet dependencies:
   - **Windows**: `powershell -File addons/galachain_wallet/setup.ps1`
   - **macOS/Linux**: `bash addons/galachain_wallet/setup.sh`
3. Build the C# project in Godot (**Build > Build Project**)
4. Enable the plugin: **Project > Project Settings > Plugins > GalaChain Wallet**
5. Add a `Control` node to your scene as a wallet mount point and call `wallet.OpenWallet(mount)`

Full integration instructions are in **[addons/galachain_wallet/INTEGRATION_GUIDE.md](godot-wallet/addons/galachain_wallet/INTEGRATION_GUIDE.md)**.

## Documentation

| Document | Purpose |
|----------|---------|
| [AGENTS.md](AGENTS.md) | Project overview, architecture, scope boundary, coding patterns, and design decisions. The primary reference for contributors and AI agents. |
| [addons/galachain_wallet/INTEGRATION_GUIDE.md](godot-wallet/addons/galachain_wallet/INTEGRATION_GUIDE.md) | Developer-facing guide for integrating the plugin into a game, with C# and GDScript examples. |
| [PROGRESS.md](PROGRESS.md) | Chronological change log with implementation details for every feature and bug fix. |
| [PROJECT-ANALYSIS.md](PROJECT-ANALYSIS.md) | Original architecture analysis, critical issues, and prioritized recommendations (mostly historical now — nearly all items resolved). |
| [FEEDBACK-EVALUATION.md](FEEDBACK-EVALUATION.md) | Evaluation of testing feedback and plans for each suggested change. |
| [GDSCRIPT-COMPATIBILITY.md](GDSCRIPT-COMPATIBILITY.md) | Notes on how GDScript compatibility is implemented via the `WalletBridge` autoload. |
| [godot-galachain-wallet-mvp-blueprint.md](godot-galachain-wallet-mvp-blueprint.md) | Original design blueprint. Referenced throughout implementation. |

## Project Structure

```
godot-wallet/                       <-- Godot project root
  addons/galachain_wallet/          <-- distributable addon
    plugin.cfg
    WalletPlugin.cs
    scenes/
      GalaChainWallet.tscn
    Scripts/
      Core/                         <-- services, signing, storage, policy registry
      Models/                       <-- DTOs, state, network result types
      UI/                           <-- wallet UI controller (partial class files)
    setup.sh / setup.ps1            <-- NuGet dependency setup scripts
    INTEGRATION_GUIDE.md
  WalletDemoGame.tscn               <-- demo scene (not part of addon)
  WalletDemoGame.cs
  Tests/                            <-- xUnit tests (not part of addon)
  project.godot
```

The `addons/galachain_wallet/` folder is what developers distribute into their own projects. The demo game and tests live outside the addon.

## Technology

- **Godot 4.6.2** with C#/.NET 8.0 support
- **Nethereum** (Accounts, HDWallet, Signer) for secp256k1 + keccak256 + BIP39
- **.NET cryptography** (`System.Security.Cryptography`) for AES-GCM + PBKDF2
- **xUnit** for unit tests

## License

TBD
