# Project Analysis: GalaChain Wallet for Godot

Date: 2026-04-07
Scope: Full codebase review against the project blueprint

---

## 1. Project Purpose

This is an **embedded in-game GalaChain wallet** built as a Godot 4.x C#/.NET module. The goal is to give game developers a narrow, opinionated wallet that can be mounted directly inside a Godot game scene — not a general-purpose wallet, not a browser extension, not an external process.

The wallet handles:
- Creating Ethereum-style wallets (secp256k1 / BIP39 mnemonic)
- Importing wallets from private key or mnemonic phrase
- Encrypted local keystore persistence (AES-256-GCM + PBKDF2)
- Lock/unlock lifecycle with password protection
- Fetching fungible token balances from the GalaChain Gateway API
- Signing and submitting `TransferToken` transactions
- A demo game scene that exercises the wallet through a facade API

The design philosophy is **"small, opinionated, and boring"** — support exactly one signable operation (`TransferToken`), refuse everything else, and keep the signing path narrow enough to audit easily.

---

## 2. Technology Stack

| Layer | Technology |
|-------|-----------|
| Engine | Godot 4.6.2, GL Compatibility renderer, Jolt Physics |
| Language | C# / .NET 8.0 (net9.0 for Android variant) |
| Crypto | Nethereum (Accounts, HDWallet, Signer) v5.8.0 |
| Key derivation | NBitcoin (BIP39 mnemonics, HD key derivation) |
| Encryption | .NET `System.Security.Cryptography` (AES-GCM, PBKDF2) |
| Additional crypto | BouncyCastle.Cryptography (Nethereum dependency) |
| Networking | `System.Net.Http.HttpClient` |
| Serialization | `System.Text.Json` |
| Storage | Godot `FileAccess` API (`user://` path) |

---

## 3. Architecture Overview

```
WalletDemoGame (entry point scene)
    |
    v
WalletFacade (game-facing public API)
    |
    +-- WalletService (orchestrator: create, import, unlock, lock, transfer)
    |       |
    |       +-- PasswordCryptoService (AES-GCM encrypt/decrypt)
    |       +-- FileWalletStorage (disk persistence via Godot FileAccess)
    |       +-- GalaChainClient (HTTP: fetch balances)
    |       +-- GalaTransferClient (HTTP: submit signed transfers)
    |       +-- GalaSigner (canonical JSON -> keccak256 -> secp256k1 sign)
    |       +-- WalletState (in-memory state container)
    |
    v
GalaChainWallet (UI controller - Godot Control node, 623 lines)
    |
    +-- Button handlers, password dialogs, balance list, transfer dialog, log output
```

### Key Design Patterns
- **Interface segregation**: `IWalletService`, `IWalletStorage`, `IGalaChainClient`, `IGalaTransferClient` allow future substitution (e.g., mobile storage, testnet clients).
- **Facade pattern**: `WalletFacade` is the only API surface game code should touch. It lazy-loads the wallet scene and delegates to `WalletService`.
- **State object**: `WalletState` holds all in-memory state (wallet existence, unlock status, address, keys, balances).
- **Password dialog reuse**: A single `ConfirmationDialog` handles all password-gated actions via `PendingPasswordAction` enum dispatch.

---

## 4. Current Implementation Status vs Blueprint

The project has a detailed 970-line blueprint (`godot-galachain-wallet-mvp-blueprint.md`) that defines the target architecture. Here is where the implementation stands against that plan:

### Implemented (Milestones 1-4 + 6)
| Feature | Status | Notes |
|---------|--------|-------|
| Wallet creation from mnemonic | Done | 12-word BIP39, m/44'/60'/0'/0/0 derivation |
| Import from private key | Done | Hex normalization, 0x prefix handling |
| Restore from mnemonic phrase | Done | Normalizes whitespace and casing |
| Encrypted local keystore | Done | AES-256-GCM + PBKDF2-SHA256 (100k iterations) |
| Lock/unlock with password | Done | Clears in-memory keys on lock |
| Display address + copy | Done | Uses `DisplayServer.ClipboardSet` |
| Fetch fungible balances | Done | POST to GalaChain Gateway, handles locked holds |
| TransferToken signing | Done | Canonical JSON, keccak256, secp256k1 |
| TransferToken submission | Done | POST to Gateway, balance refresh after submit |
| Demo game scene | Done | Open/Close wallet, status display |
| Wallet facade for game code | Done | Narrow API surface |

### Not Implemented (from Blueprint)
| Blueprint Feature | Status | Blueprint Section |
|-------------------|--------|-------------------|
| `DtoPolicyRegistry` (allowlist enforcement) | Missing | Section 13 |
| `IntentRenderer` (confirmation model from DTO) | Missing | Section 7 |
| `WalletSessionService` (idle timeout, re-auth) | Missing | Section 7 |
| `AuditLogger` (event logging) | Missing | Section 7 |
| Auto-lock after idle period | Missing | Section 10 |
| `dtoExpiresAt` enforcement | **Missing** | Section 12, Rule 1 |
| `dtoOperation` field on requests | **Missing** | Section 12, Rule 2 |
| Dry-run preview before submit | Missing | Section 17 |
| Confirmation UI derived from DTO fields | Partial | Section 14 |
| Advanced details / raw payload view | Missing | Section 14 |
| `CanHandleOperation()` API | Missing | Section 9 |
| `GetUserRef()` API | Missing | Section 9 |
| Unit / integration tests | **Missing** | Section 21 |
| Godot addon/plugin structure | Not used | Section 8 |
| `IClipboardService` / `IClock` abstractions | Stubs only | Section 6 |
| Token metadata lookup | Missing | Section 15 |
| Network environment allowlist | Missing | Section 15 |
| Structured response model (Success/Rejected/Error) | Missing | Section 15 |

---

## 5. Critical Issues

These are issues that affect correctness or security and should be addressed before any real use.

### 5.1 `dtoExpiresAt` is Never Set (CRITICAL)

**Location**: `WalletService.cs:191-201` (`BuildTransferRequest`)

The `GalaTransferTokenRequest.dtoExpiresAt` field defaults to `0` (C# long default). It is never assigned a value. The blueprint explicitly states (Section 12, Rule 1): *"Every submitted DTO gets `dtoExpiresAt`"* with a recommended window of 2-5 minutes.

**Impact**: Transfers either fail on-chain (if GalaChain rejects `0` or expired timestamps) or are submitted without expiration protection, making them potentially replayable.

### 5.2 Mnemonic-Imported Wallets Cannot Sign After Unlock (CRITICAL)

**Location**: `WalletService.cs:91-113` (`Unlock`)

When a mnemonic-type wallet is unlocked, the code does:
```csharp
_state.PrivateKey = payload.SecretType == WalletSecretType.PrivateKey ? payload.Secret : "";
```

If the secret type is `Mnemonic`, the private key is set to `""`. The wallet shows as "unlocked" but `SubmitTransferAsync` will fail at line 208-209:
```csharp
if (string.IsNullOrWhiteSpace(_state.PrivateKey))
    throw new InvalidOperationException("Private key is not available in memory.");
```

**Fix needed**: When unlocking a mnemonic-type wallet, re-derive the private key from the mnemonic using `Nethereum.HdWallet.Wallet`, the same way `ImportMnemonic` does.

### 5.3 Potential NullReferenceException on Startup

**Location**: `GalaChainWallet.cs:95`

```csharp
_walletService.LoadWalletMetadataIfPresent();  // line 95 - no null check
// ...
if (_walletService != null)  // line 98 - null check comes AFTER the call
```

If `Initialize()` hasn't been called before `_Ready()` runs (which depends on Godot's scene loading order), `_walletService` is null and this will throw.

### 5.4 `signature` Field Included in Canonical JSON Signing

**Location**: `GalaSigner.cs:9-17`

The signer creates an anonymous object with specific fields to sign, which is a reasonable workaround. However, it hardcodes the field list. The `GalaCanonicalJson` serializer itself has no logic to exclude `signature` or `trace` as the blueprint requires (Section 16). If anyone later passes the full `GalaTransferTokenRequest` to the serializer instead of the anonymous object, the signature field (which contains `""` by default) would be included in the hash, producing an invalid signature.

### 5.5 ~~No `dtoOperation` Field~~ (Deferred)

GalaChain's current Gateway API does not reliably support a `dtoOperation` field on submitted DTOs. Operation routing is handled by the endpoint URL, not a DTO field. The blueprint's Rule 2 (Section 12) has been updated to reflect this. Revisit if GalaChain adds explicit `dtoOperation` validation in a future release.

---

## 6. Bugs

### 6.1 File Name Swap: Interface and Implementation Reversed

- `GalaTransferClient.cs` contains `interface IGalaTransferClient` (the interface)
- `IGalaTransferClient.cs` contains `class GalaTransferClient` (the implementation)

These file names are swapped. Convention is that `IFoo.cs` contains the interface and `Foo.cs` contains the class.

### 6.2 Typo in Validation Error Message

**Location**: `GalaChainWallet.cs:453`

```csharp
error = "Recipient address must start with eht| or client|.";
```

`eht|` should be `eth|`.

### 6.3 Double Brace in Log Method

**Location**: `GalaChainWallet.cs:609-613`

```csharp
private void Log(string message){
    {
        _logOutput.AppendText($"{message}\n");
    }
}
```

Extra `{` `}` block inside the method body. Harmless but indicates a copy-paste artifact.

### 6.4 Unused Method: `BuildStatusText()`

**Location**: `GalaChainWallet.cs:555-570`

`BuildStatusText()` is defined but never called. `RefreshUi()` computes status text inline.

### 6.5 Double `LoadWalletMetadataIfPresent()` Call

`WalletFacade` constructor calls `_walletService.LoadWalletMetadataIfPresent()` (line 13), and `GalaChainWallet._Ready()` also calls it (line 95). This means wallet metadata is loaded twice on startup — harmless but wasteful.

### 6.6 Transfer URL Not on Config Object

`GalaChainNetworkConfig` has a computed `FetchBalancesUrl` property, but the transfer URL is built inline in `GalaTransferClient.TransferAsync()`. Inconsistent — both should be on the config.

---

## 7. Security Analysis

### What's Done Well
- **AES-256-GCM encryption** with proper nonce/tag handling and PBKDF2 key derivation at 100k iterations.
- **Key material zeroing**: `CryptographicOperations.ZeroMemory()` is called on `key` and `plaintext` byte arrays after encryption/decryption.
- **Recovery phrase shown once**: `ConsumePendingRecoveryPhrase()` clears the phrase after display.
- **Private key never exposed to game code**: The `WalletFacade` has no method to retrieve keys.
- **Self-transfer prevention**: UI validates that recipient != sender.
- **Positive quantity validation**: UI checks `quantity > 0` and `quantity <= available`.

### Security Gaps
| Gap | Severity | Blueprint Reference |
|-----|----------|-------------------|
| No `dtoExpiresAt` — transactions have no time bound | High | Section 12, Rule 1 |
| No `dtoOperation` — DTO type not explicitly labeled | Medium | Section 12, Rule 2 |
| No DTO policy enforcement — any DTO structure could be submitted | Medium | Section 13 |
| No idle timeout / auto-lock | Medium | Section 10 |
| No signature verification before submit (recovered addr check) | Low | Section 16 |
| Private key in managed .NET strings (not pinned/zeroed on lock) | Low | Acknowledged in blueprint |
| No HTTP timeout configuration on `HttpClient` | Low | — |
| No network environment allowlist | Low | Section 15 |
| Mnemonic stored as .NET string (immutable, GC-managed) | Low | Inherent .NET limitation |

### Threat Model Notes
The blueprint honestly acknowledges that this MVP does not defend against a compromised host OS, malware, memory scraping, or supply-chain attacks. These are reasonable exclusions for a desktop MVP. The primary value of the security model is preventing **accidental** misuse by game code and providing reasonable at-rest encryption for the keystore file.

---

## 8. Architectural Concerns

### 8.1 Monolithic UI Controller

`GalaChainWallet.cs` is 623 lines handling all UI state, password dialogs, balance display, transfer validation, and async operations. The blueprint envisioned separate scenes and controllers:
- `WalletPanelController.cs`
- `ConfirmTransferController.cs`
- `CreateWalletDialog.tscn`
- `ImportWalletDialog.tscn`
- `UnlockDialog.tscn`

This is fine for an MVP, but will become unwieldy as features are added.

### 8.2 No Godot Addon Structure

The blueprint specified an `addons/galachain_wallet/` plugin structure with a `plugin.cfg` and `WalletPlugin.cs`. The current implementation uses a flat `Scripts/` directory inside the main project. This means the wallet is not distributable as a Godot addon — it's tightly coupled to this specific project.

### 8.3 No Namespace Usage

All 28 C# files declare classes in the global namespace. This will cause naming collisions if the wallet is embedded in a game that also has classes named `WalletState`, `TokenBalanceModel`, etc. The blueprint implied namespace isolation through the addon structure.

### 8.4 Concrete Dependencies in WalletService

`WalletService` hard-codes its dependencies:
```csharp
private readonly IWalletStorage _storage = new FileWalletStorage();
private readonly IGalaChainClient _galaChainClient = new GalaChainClient();
private readonly IGalaTransferClient _galaTransferClient = new GalaTransferClient();
```

The interfaces exist but there's no dependency injection — the concrete types are baked in. This makes it impossible to substitute a testnet client, a mock storage, or test implementations without modifying the class.

### 8.5 No Structured Network Responses

The blueprint called for `Success<T>`, `Rejected`, `TransportError`, `ValidationError` response types. Currently, network errors are communicated via exceptions with raw HTTP body messages. This makes it hard for the UI to provide user-friendly error messages.

---

## 9. Code Quality Notes

### Strengths
- Clean separation of concerns between Core, Models, UI, and Services directories.
- Interfaces defined for all major service boundaries.
- Consistent coding style across files.
- Sensible use of Godot's `%` unique name syntax for node references.
- Good password dialog reuse pattern via `PendingPasswordAction` enum.

### Areas for Improvement
- No XML doc comments on public APIs.
- `ClipboardService.cs` and `WalletAddressModel.cs` are empty stubs — should be removed or implemented.
- `GalaFetchBalancesResponse` and `GalaFetchBalancesRequest` models not reviewed but likely simple DTOs.
- `WalletState` is a mutable class with no encapsulation — any code with a reference can mutate it.
- Balance `Instance` is hardcoded to `"0"` in `GalaChainClient.cs:82` — this only works for fungible tokens.

---

## 10. Development Progress (Git History)

The project was built incrementally over 9 commits, tracking cleanly to the blueprint milestones:

| Commit | Milestone | Description |
|--------|-----------|-------------|
| `bcad77b` | M1 | Project scaffold |
| `3ef9d55` | M1 | UI button structure |
| `3a8b10a` | M2 | Wallet generation + import (Nethereum) |
| `67b1776` | M2 | Encrypted keystore, password, lock/unlock |
| `0ce7ea0` | M2 | Mnemonic import/restore |
| `6f16f0c` | M3 | GalaChain balance fetch |
| `a5f4b81` | M4 | Transfer internals (signer, canonical JSON) |
| `fc8c5b4` | M4 | Transfer submission flow |
| `151380c` | M6 | Game-wallet facade integration |

**Not started**: Milestone 5 (safety hardening) is entirely unimplemented — no auto-lock, no dry-run, no raw DTO view, no log scrubbing.

---

## 11. Prioritized Recommendations

### Must Fix (Correctness / Security)
1. **Set `dtoExpiresAt`** on every transfer request (e.g., `DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds()`).
2. **Re-derive private key from mnemonic on unlock** — currently, mnemonic wallets are unlocked but unable to sign.
3. **Fix the NullReferenceException** in `GalaChainWallet._Ready()` — guard or reorder the `LoadWalletMetadataIfPresent()` call.
4. **Add `dtoOperation`** field to `GalaTransferTokenRequest` and set it explicitly.

### Should Fix (Quality / Blueprint Compliance)
5. Fix the `GalaTransferClient.cs` / `IGalaTransferClient.cs` filename swap.
6. Fix the `"eht|"` typo in validation error message.
7. Add a `TransferUrl` computed property to `GalaChainNetworkConfig` for consistency.
8. Make `GalaCanonicalJson` exclude `signature` and `trace` fields from serialization.
9. Add constructor-based dependency injection to `WalletService`.
10. Add C# namespaces (e.g., `GalaWallet.Core`, `GalaWallet.Models`, `GalaWallet.UI`).

### Should Implement (Blueprint Milestone 5)
11. Idle timeout / auto-lock.
12. DTO policy registry for operation allowlisting.
13. Structured network response types.
14. Unit tests — at minimum for the serializer, signer, and crypto service.
15. Golden vector tests for canonical JSON serialization.

### Nice to Have
16. Break up `GalaChainWallet.cs` into smaller UI controllers.
17. Refactor into Godot addon structure for distribution.
18. Remove dead code (`BuildStatusText`, empty stubs).
19. Add HTTP timeout configuration.
20. Implement signature verification (recover signer address) before submitting.

---

## 12. Summary

This is a well-structured MVP that successfully implements the core wallet lifecycle (create, import, restore, lock, unlock), encrypted persistence, balance fetching, and token transfers against GalaChain mainnet. The architecture is clean with good interface boundaries, and the crypto implementation (AES-GCM, PBKDF2, secp256k1 signing) uses mature, appropriate libraries.

The two critical gaps are **missing `dtoExpiresAt`** (transactions have no time bound) and **mnemonic wallets breaking on unlock** (private key not re-derived). These should be fixed before any real-world use.

The project is approximately through **Milestone 4** of the 6-milestone blueprint. Milestone 5 (safety hardening) is entirely unimplemented, meaning the opinionated-signer security model described in the blueprint is not yet enforced — there is no DTO policy registry, no operation allowlisting, no idle timeout, and no dry-run support. The demo game (Milestone 6) is functional but minimal.

Overall, this is a solid foundation with clear next steps.
