# AGENTS.md — GalaChain Wallet for Godot

## Project Overview

Embedded in-game GalaChain wallet built as a Godot 4.x C#/.NET module. Opinionated signer — only supports allowlisted operations (currently `TransferToken`), rejects everything else. Targets Windows desktop first with abstractions for cross-platform later.

## Key Documents

- `godot-galachain-wallet-mvp-blueprint.md` — Design blueprint. All implementation decisions should reference this.
- `PROJECT-ANALYSIS.md` — Architecture analysis and prioritized recommendations.
- `PROGRESS.md` — Change log with implementation details. Update this when making changes.

## Architecture

```
WalletDemoGame (entry point)
  -> WalletFacade (game-facing API, narrow surface)
    -> WalletService (orchestrator, owns WalletState)
      -> DtoPolicyRegistry (validates operations via ITransactionPolicy)
      -> PasswordCryptoService (AES-256-GCM + PBKDF2)
      -> FileWalletStorage (encrypted keystore at user://wallet/wallet.json)
      -> GalaChainClient (balance fetch, dry-run)
      -> GalaTransferClient (transfer submission)
      -> GalaSigner (canonical JSON -> keccak256 -> secp256k1)
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
- **Godot addon structure**: Not yet implemented. Currently flat `Scripts/` layout. Target `addons/galachain_wallet/` when ready to distribute.
- **DTO policy registry**: Implemented but currently single-operation. Will become more valuable when adding BurnToken, NFT transfers, etc.
- **Mobile secure storage**: Out of scope for desktop MVP. `IWalletStorage` interface exists for future platform-specific implementations.
