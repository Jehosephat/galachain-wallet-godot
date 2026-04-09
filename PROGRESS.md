# GalaChain Wallet for Godot — Progress Log

This document tracks changes made after the initial MVP implementation (commit `151380c`).

---

## 2026-04-07

### Wallet UI: Visual fixes for embedded use
- **Problem**: Wallet scene was designed as a standalone main scene. When instantiated inside the demo game's `WalletMount`, it had no background, layout was broken, and balances/log sections collapsed to zero height.
- **Changes**:
  - Added a `Panel` background node to `GalaChainWallet.tscn` so the wallet is opaque when overlaid on game content.
  - Changed `WalletMount` in `WalletDemoGame.tscn` from a 40x40px centered box to full-rect anchors so the wallet has room to lay out.
  - Set `mouse_filter = 2` (Ignore) on `WalletMount` so it doesn't block clicks on game buttons underneath.
  - Added `custom_minimum_size` to `BalancesList` (100px) and `LogOutput` (80px) to prevent them collapsing.
  - Anchored the wallet to the right half of the screen (`anchor_left = 0.5`) for a side-panel modal style.

### Game-initiated transfer flow
- **Problem**: The blueprint calls for games to be able to request transfers through the wallet facade. There was no way for game code to prompt a pre-filled transfer.
- **Changes**:
  - Added `RequestTransfer(toAddress, quantity, tokenSymbol)` to `GalaChainWallet` — finds the token by symbol in loaded balances and opens the transfer dialog pre-filled.
  - Extracted `OpenTransferDialog()` helper from `OnTransferPressed()` to share dialog setup logic.
  - Added `RequestTransfer()` passthrough on `WalletFacade`.
  - Added "Make Purchase: 15 GALA" button to `WalletDemoGame` that sends a transfer request to `client|5f58d8641586e117c5e68834`.
  - **Pending transfer on locked wallet**: If the wallet is locked when a transfer is requested, the request is stashed (`_pendingTransferTo`, `_pendingTransferQuantity`, `_pendingTransferSymbol`) and the unlock dialog pops up automatically. After successful unlock + balance refresh, `ConsumePendingTransfer()` replays the request and opens the pre-filled transfer dialog.

### Window size increase
- **Problem**: Default Godot window (1152x648) was too short — wallet content clipped at top and bottom.
- **Change**: Set `window/size/viewport_width=1280` and `window/size/viewport_height=900` in `project.godot`.

### Security fix: `dtoExpiresAt` enforcement
- **Problem**: `BuildTransferRequest()` in `WalletService.cs` never set `dtoExpiresAt`, leaving it at `0`. This violated the blueprint's core security rule (Section 12, Rule 1) that every submitted DTO must have a short expiration window.
- **Change**: `dtoExpiresAt` is now set to `DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds()` — a 3-minute window, within the blueprint's recommended 2–5 minute range.
- **File**: `WalletService.cs:BuildTransferRequest()`

### Bug fix: Mnemonic wallets unable to sign after unlock
- **Problem**: When unlocking a mnemonic-type wallet, `Unlock()` set `_state.PrivateKey = ""` because the decrypted secret was the mnemonic, not the private key. The wallet appeared unlocked but `SubmitTransferAsync` would throw "Private key is not available in memory."
- **Change**: When the decrypted secret type is `Mnemonic`, the private key is now re-derived using `Nethereum.HdWallet.Wallet` at derivation path `m/44'/60'/0'/0/0` — the same path used during `ImportMnemonic` and `CreateWallet`.
- **File**: `Scripts/Core/WalletService.cs:Unlock()`

### Bug fix: NullReferenceException in `GalaChainWallet._Ready()`
- **Problem**: `_walletService.LoadWalletMetadataIfPresent()` was called at line 99 before the null check at line 102. If `Initialize()` hadn't been called before `_Ready()` (depends on Godot scene loading order), this would throw a `NullReferenceException`.
- **Change**: Moved `LoadWalletMetadataIfPresent()` inside the existing `_walletService != null` guard.
- **File**: `Scripts/UI/GalaChainWallet.cs:_Ready()`

### Dry-run transfer preview with inline fee display
- **Problem**: Blueprint (Section 17) specifies an optional dry-run preview before submitting transfers. Users had no way to see the expected fee before committing.
- **Changes**:
  - Added `DryRunUrl` computed property to `GalaChainNetworkConfig` (`/DryRun` endpoint).
  - Added `GalaDryRunResponse`, `GalaDryRunData`, `GalaDryRunInnerResponse` models to parse the dry-run API response.
  - Added `TransferPreviewResult` model — the service-layer return type with `WouldSucceed`, `Message`, `EstimatedFee`, `FeeToken`.
  - Added `DryRunTransferAsync` to `IGalaChainClient`/`GalaChainClient` — POSTs an unsigned DTO to the DryRun endpoint, extracts fees from the `writes` dictionary.
  - Added `PreviewTransferAsync` to `IWalletService`/`WalletService` — builds an unsigned preview DTO and calls dry-run (no signing needed for simulation).
  - **UI**: When inputs validate in the transfer dialog, a dry-run fires asynchronously and the estimated fee appears inline in the summary label (e.g., "Estimated fee: 1 GALA"). The user sees the fee before clicking OK.
  - Fixed TransferDialog title from "Wallet Password" to "Confirm Transfer".
- **GalaChain DryRun API details discovered during implementation**:
  - Endpoint: `POST /api/{channel}/{contract}/DryRun`
  - Request: `{ method: "TransferToken", signerAddress: "eth|...", dto: { ...transfer fields... } }` — inner DTO fields must be nested under a `dto` property, not flat at the top level.
  - No signature required for dry-run; `signerAddress` identifies the caller.
  - Response: `{ Status: 1, Data: { response: {...}, reads: {...}, writes: {...}, deletes: {...} } }`
  - Fee extraction: scan `writes` for keys containing `GCFR` (fee record), parse JSON value, read `quantity` field. Fee is in GALA (burned).
- **Files**: `GalaChainNetworkConfig.cs`, `GalaDryRunResponse.cs` (new), `TransferPreviewResult.cs` (new), `IGalaChainClient.cs`, `GalaChainClient.cs`, `IWalletService.cs`, `WalletService.cs`, `GalaChainWallet.cs`, `GalaChainWallet.tscn`

### Quality / Blueprint compliance batch
Six fixes applied in one pass:

1. **File name swap fixed**: `GalaTransferClient.cs` now contains the class, `IGalaTransferClient.cs` now contains the interface. Contents were swapped to match standard C# naming convention.

2. **Typo fixed**: `"eht|"` corrected to `"eth|"` in the transfer validation error message in `GalaChainWallet.cs`.

3. **`TransferTokenUrl` added to `GalaChainNetworkConfig`**: Computed property matching the existing `FetchBalancesUrl` pattern. `GalaTransferClient` now uses it instead of building the URL inline.

4. **`GalaCanonicalJson` now excludes `signature` and `trace`**: Added a `HashSet<string> ExcludedFields` and an `isRoot` parameter to `Normalize()`. Top-level properties named `signature` or `trace` are skipped during serialization. This means the full `GalaTransferTokenRequest` can now be passed directly to the serializer without the anonymous-object workaround (though the signer still uses the explicit field list for clarity).

5. **Constructor-based DI on `WalletService`**: All dependencies (`PasswordCryptoService`, `IWalletStorage`, `IGalaChainClient`, `IGalaTransferClient`, `GalaSigner`) are now injected via optional constructor parameters with sensible defaults. This enables substitution for testing, testnet configs, or mock implementations without modifying the class.

6. **C# namespaces added to all files**:
   - `GalaWallet.Models` — all DTOs, enums, state models (12 files)
   - `GalaWallet.Core` — services, interfaces, signing, storage (13 files)
   - `GalaWallet.UI` — wallet UI controller (1 file)
   - `GalaWallet.Services` — clipboard stub (1 file)
   - `WalletDemoGame` left in global namespace (entry point)
   - Cross-namespace `using` directives added where needed.
   - File-scoped namespace syntax (`namespace X;`) used throughout.

**Build verified**: `dotnet build` succeeds with 0 errors (21 pre-existing nullable warnings).

## 2026-04-09

### Idle timeout / auto-lock
- **Problem**: Blueprint (Section 10) requires the wallet to auto-lock after a configurable idle period to limit the window during which keys are in memory.
- **Changes**:
  - Added `IdleTimeoutSeconds` constant (300s / 5 minutes) and `_lastActivityTime` tracker to `GalaChainWallet`.
  - Added `_Process` override that checks elapsed time since last activity; if the wallet is unlocked and idle time exceeds the threshold, it calls `Lock()`, refreshes the UI, and logs "Wallet auto-locked after inactivity."
  - Added `ResetIdleTimer()` calls at all wallet action points: password dialog confirmation (covers create, import, unlock), balance refresh, copy address, transfer dialog open, and transfer dialog confirmation.
  - Timer initializes on `_Ready` and resets on every user-initiated wallet action.
- **File**: `Scripts/UI/GalaChainWallet.cs`

### DTO policy registry for operation allowlisting
- **Problem**: Blueprint (Section 13) calls for an explicit registry of supported operations so the wallet can formally reject unknown DTOs and centralize validation logic per operation.
- **Changes**:
  - Added `ITransactionPolicy` interface with `OperationId` and `Validate(TransactionContext)` — each supported operation implements this to define its own validation rules.
  - Added `TransactionContext` (from/to address, quantity, available balance) and `ValidationResult` (IsValid, Error) supporting types.
  - Added `TransferTokenPolicy` — implements all TransferToken validation (recipient format, self-transfer check, quantity parsing/bounds). This logic was previously inline in the UI's `TryBuildTransferDraft`.
  - Added `DtoPolicyRegistry` — holds registered policies in a dictionary, auto-registers `TransferTokenPolicy` on construction. `Validate(operationId, context)` rejects unknown operations with "Unsupported operation: X".
  - Injected `DtoPolicyRegistry` into `WalletService` via constructor DI.
  - Added `ValidateTransfer(draft, availableBalance)` to `IWalletService`/`WalletService` — builds a `TransactionContext` and delegates to the registry.
  - Refactored `TryBuildTransferDraft` in `GalaChainWallet.cs` to delegate all validation to `_walletService.ValidateTransfer()` instead of inline checks.
- **New files**: `ITransactionPolicy.cs`, `TransferTokenPolicy.cs`, `DtoPolicyRegistry.cs`
- **Modified files**: `IWalletService.cs`, `WalletService.cs`, `GalaChainWallet.cs`
- **To add a new operation later**: implement `ITransactionPolicy`, register it in `DtoPolicyRegistry`, and add the corresponding service/client methods.

---

## Known issues remaining
See `PROJECT-ANALYSIS.md` Section 5 for the full critical issues list. Key items still open:
- ~~Mnemonic-imported wallets cannot sign after unlock (private key not re-derived)~~ — Fixed
- ~~No `dtoOperation` field on transfer requests~~ — **Deferred**: GalaChain's current Gateway API does not reliably support this field; operation routing is handled by endpoint URL. Revisit if GalaChain adds support.
- ~~`GalaCanonicalJson` does not exclude `signature`/`trace` fields~~ — Fixed
- ~~No DTO policy registry / allowlist enforcement~~ — Fixed
- ~~No idle timeout / auto-lock~~ — Fixed (5-minute timeout)
- No unit tests
- ~~Interface/class file name swap (`GalaTransferClient.cs` ↔ `IGalaTransferClient.cs`)~~ — Fixed
