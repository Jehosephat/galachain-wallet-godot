# AGENTS.md — GalaChain Wallet for Godot

## Project Overview

Embedded in-game GalaChain wallet built as a Godot 4.x C#/.NET module. Opinionated signer — only supports allowlisted operations (currently `TransferToken`), rejects everything else. Targets Windows desktop first with abstractions for cross-platform later.

## Scope Boundary

This plugin is **strictly client-side** and handles only operations the player themselves signs with their own wallet key:

**In scope:**
- Player wallet UI (create, import, unlock, lock)
- Player key management and encrypted keystore
- Balance display and refresh
- Player-signed transfers (the player approves and signs each transaction)
- Dry-run fee estimation for player transactions

**Out of scope — belongs on a game backend server:**
- Minting tokens to players (requires authority/mint allowance, which must never ship in client code)
- Burning tokens on players' behalf
- Granting rewards or airdrops
- Any operation signed with a key other than the player's wallet
- Any operation that requires permissions a player's wallet shouldn't have

Game backends should talk to GalaChain directly using their own stack. This plugin does not provide shared SDK utilities for backend operations — if a game needs to mint rewards, the backend does that, and the client just calls `RefreshBalancesAsync()` afterward to pick up the new tokens.

## Key Documents

- `godot-galachain-wallet-mvp-blueprint.md` — Design blueprint. All implementation decisions should reference this.
- `PROJECT-ANALYSIS.md` — Architecture analysis and prioritized recommendations.
- `PROGRESS.md` — Change log with implementation details. Update this when making changes.
- `addons/galachain_wallet/INTEGRATION_GUIDE.md` — Developer-facing plugin usage guide. Update this when adding new features, API methods, or configuration options.

## Project Structure

The wallet is a Godot addon. Developers distribute `addons/galachain_wallet/` into their project.

```
addons/galachain_wallet/          <-- distributable addon
  plugin.cfg
  WalletPlugin.cs
  scenes/
    GalaChainWallet.tscn
  Scripts/
    Core/                         <-- services, interfaces, signing, storage, policy
    Models/                       <-- DTOs, enums, state, network result types
    UI/                           <-- wallet UI controller (3 partial class files)
WalletDemoGame.tscn               <-- demo (not part of addon)
WalletDemoGame.cs
Tests/                            <-- xUnit tests (not part of addon)
```

## Architecture

```
WalletDemoGame (entry point, outside addon)
  -> WalletFacade (game-facing API, narrow surface)
    -> WalletService (orchestrator, owns WalletState)
      -> DtoPolicyRegistry (validates operations via ITransactionPolicy)
      -> PasswordCryptoService (AES-256-GCM + PBKDF2)
      -> FileWalletStorage (encrypted keystore at user://wallet/wallet.json)
      -> GalaChainClient (balance fetch, dry-run)
      -> GalaTransferClient (transfer submission)
      -> GalaSigner (canonical JSON -> keccak256 -> secp256k1, self-verifying)
    -> GalaChainWallet (UI controller, Godot Control node)
```

## Namespaces

- `GalaWallet.Core` — Services, interfaces, signing, storage, policy registry
- `GalaWallet.Models` — DTOs, enums, state models, network result types
- `GalaWallet.UI` — Wallet UI controller
- `GalaWallet.Tests` — xUnit test project (not distributed with plugin)
- `WalletDemoGame` — Global namespace (demo entry point, not part of the wallet module)

## Coding Patterns

### UI controller split via partial classes
`GalaChainWallet` is a single Godot node with one script, split across 3 files using C# `partial class`:
- `GalaChainWallet.cs` — Fields, initialization, lifecycle, state display, logging
- `GalaChainWallet.WalletActions.cs` — Create, import, unlock, lock, password dialog
- `GalaChainWallet.Transfer.cs` — Transfer flow, dry-run preview, validation, submit

### Network calls return `NetworkResult<T>`, not exceptions
All network operations return `NetworkResult<T>` with `Success`, `Rejected`, `TransportError`, or `ParseError` variants. The UI checks `result.IsSuccess` — no try/catch for network errors.

### Validation goes through the policy registry
Transfer validation lives in `TransferTokenPolicy`, not scattered in UI code. The UI calls `_walletService.ValidateTransfer(draft, availableBalance)` which delegates to `DtoPolicyRegistry`. To add a new operation: implement `ITransactionPolicy`, register it in `DtoPolicyRegistry`.

### Constructor DI with optional defaults
`WalletService` takes all dependencies via optional constructor parameters with sensible defaults (`new WalletService()` works out of the box). This enables test substitution without a DI container.

### Canonical JSON serialization
`GalaCanonicalJson.Serialize()` is the single serialization path for signing. It sorts keys alphabetically, camelCases property names, and excludes `signature` and `trace` fields at the root level. Do not create alternative serializers.

### Transfer request building
`WalletService.BuildTransferRequest()` is the single place that constructs `GalaTransferTokenRequest`. It sets `dtoExpiresAt` (3-minute window), generates a fresh `uniqueKey`, and resolves the `from` address. The signer then signs this DTO.

### Dry-run for fee estimation
The dry-run uses the GalaChain `/DryRun` endpoint. The request wraps the transfer DTO inside a `dto` property alongside `method` and `signerAddress` at the top level. No signature required. Fees are extracted from the `writes` dictionary by scanning for `GCFR` key entries.

### Wallet events for game code
Events originate on `GalaChainWallet` (the UI class, where actions happen) and are forwarded through `WalletFacade` for game code to consume. This keeps the dependency one-way: facade -> UI.

**To add a new event:**
1. Add `public event Action<...>? EventName;` to `GalaChainWallet.cs` (with the other events)
2. Fire it at the right point in the UI code: `EventName?.Invoke(...);`
3. Add the same event signature to `WalletFacade`
4. Forward it in `WalletFacade.SubscribeToWalletEvents()`: `wallet.EventName += args => EventName?.Invoke(args);`

**Current events:**
| Event | Args | Fired when |
|-------|------|-----------|
| `WalletCreated` | `string address` | After wallet creation |
| `WalletImported` | `string address` | After private key or mnemonic import |
| `WalletUnlocked` | `string address` | After successful unlock |
| `WalletLocked` | (none) | After manual lock or auto-lock |
| `TransferCompleted` | `string to, string qty, string symbol` | After successful transfer submission |
| `TransferFailed` | `string error` | After failed transfer |
| `BalancesRefreshed` | (none) | After balances are updated (any trigger) |

**Game code usage:**
```csharp
_wallet.BalancesRefreshed += () => { /* update game UI */ };
_wallet.TransferCompleted += (to, qty, sym) => { /* grant item, etc. */ };
```

### GDScript compatibility via WalletBridge
`WalletBridge` (Node) wraps `WalletFacade` for GDScript interop. Registered as autoload singleton "Wallet" by `WalletPlugin`. C# games use `WalletFacade` directly; GDScript games use the `Wallet` autoload. When adding new facade methods or events, also add them to `WalletBridge` — signals for events, Godot-compatible types for return values (`Array<Dictionary>` instead of `List<T>`).

### Idle timeout
The wallet auto-locks after 5 minutes of inactivity (`IdleTimeoutSeconds = 300`). The idle timer resets on every user-initiated wallet action. `_Process` checks the timer each frame when the wallet is unlocked.

## GalaChain API Details

- **Gateway**: `https://gateway-mainnet.galachain.com/api`
- **Balance fetch**: `POST /asset/token-contract/FetchBalances` — body: `{ owner: "eth|<address>" }`
- **Transfer submit**: `POST /asset/token-contract/TransferToken` — body: signed DTO
- **Dry-run**: `POST /asset/token-contract/DryRun` — body: `{ method: "TransferToken", signerAddress: "eth|...", dto: { ...unsigned transfer fields... } }`
- **`dtoOperation` field**: Deferred — GalaChain Gateway does not reliably support this. Operation routing is via endpoint URL.
- **Address format**: `eth|<address_without_0x_prefix>` or `client|<id>`
- **Derivation path**: `m/44'/60'/0'/0/0` (standard Ethereum)

## Testing

- Test project: `godot-wallet/Tests/` — xUnit, links source files directly (no Godot SDK dependency)
- Run: `dotnet test godot-wallet/Tests/`
- Tests cover: canonical JSON serializer, secp256k1 signer, AES-GCM crypto service
- The `Tests/` directory is NOT part of the plugin distribution

## Decisions and Deferrals

- **`dtoOperation`**: Deferred — GalaChain Gateway doesn't support it. Revisit if GalaChain adds validation.
- **Godot addon structure**: Implemented. All wallet code lives under `addons/galachain_wallet/`. Demo and tests stay outside.
- **DTO policy registry**: Implemented but currently single-operation. Will become more valuable when adding BurnToken, NFT transfers, etc.
- **Mobile secure storage**: Out of scope for desktop MVP. `IWalletStorage` interface exists for future platform-specific implementations.
