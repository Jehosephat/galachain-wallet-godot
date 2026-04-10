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

### Structured network response types
- **Problem**: Blueprint (Section 15) calls for normalized network responses instead of raw exceptions. All three network operations (`FetchBalances`, `TransferToken`, `DryRun`) used `throw new InvalidOperationException(...)` for errors, forcing the UI to use try/catch with raw exception messages.
- **Changes**:
  - Added `NetworkResult<T>` generic result type with factory methods: `Success(data)`, `Rejected(message, statusCode)`, `TransportError(message)`, `ParseError(message)`.
  - Added `NetworkErrorKind` enum: `Rejected`, `TransportError`, `ParseError`.
  - Updated `IGalaChainClient` — `FetchBalancesAsync` returns `NetworkResult<List<TokenBalanceModel>>`, `DryRunTransferAsync` returns `NetworkResult<TransferPreviewResult>`.
  - Updated `IGalaTransferClient` — `TransferAsync` returns `NetworkResult<string>`.
  - Updated `GalaChainClient` and `GalaTransferClient` — all methods now catch `HttpRequestException` and return `TransportError`, return `Rejected` for non-success HTTP status codes, and return `ParseError` for unparseable responses. No more thrown exceptions for network failures.
  - Updated `IWalletService`/`WalletService` — `RefreshBalancesAsync` returns `NetworkResult<List<TokenBalanceModel>>`, `PreviewTransferAsync` returns `NetworkResult<TransferPreviewResult>`, `SubmitTransferAsync` returns `NetworkResult<string>`.
  - Updated `GalaChainWallet.cs` — all UI handlers now check `result.IsSuccess` and display `result.ErrorMessage` instead of catching exceptions. The dry-run preview distinguishes between transport errors and rejection errors in the fee display.
- **New file**: `Scripts/Models/NetworkResult.cs`
- **Modified files**: `IGalaChainClient.cs`, `GalaChainClient.cs`, `IGalaTransferClient.cs`, `GalaTransferClient.cs`, `IWalletService.cs`, `WalletService.cs`, `GalaChainWallet.cs`

### Golden vector tests for canonical JSON serialization
- **Problem**: Blueprint (Section 21) requires golden vector tests — verified against known GalaChain inputs/outputs — to prove wire-compatibility of the serializer and signer.
- **Changes**:
  - Added `GoldenVectorTests.cs` with 5 tests verified against real mainnet transaction: block 9627175, TX `50fef561...` ([explorer link](https://explorer.galachain.com/details/asset-channel/9627175)).
  - **CanonicalJson_MatchesExpectedOutput** — exact string match of our serializer output against the expected canonical form of the on-chain DTO.
  - **CanonicalJson_ExcludesSignatureAndTrace** — verifies both fields are stripped from a real DTO that contained them.
  - **Keccak256Hash_IsConsistentWithSignature** — computes keccak256 of our canonical JSON, recovers the Ethereum address from the on-chain signature, and verifies it matches the signer's address (`0xcbf9a4A8b541177CD762d61f561Be4aF65561677` from the GCUP read set).
  - **CanonicalJson_KeyOrder_MatchesGalaChainExpectation** — verifies alphabetical root key ordering.
  - **CanonicalJson_NestedKeyOrder_MatchesGalaChainExpectation** — verifies alphabetical key ordering within tokenInstance.
- **Result**: 26 total tests, all passing.
- **New file**: `Tests/GoldenVectorTests.cs`

### Unit tests for serializer, signer, and crypto service
- **Problem**: Blueprint (Section 21) requires unit tests for the core signing and crypto components. None existed.
- **Changes**:
  - Created `Tests/` xUnit test project (`Godot-Wallet.Tests.csproj`) that links source files directly from the main project to avoid Godot SDK dependency issues.
  - **GalaCanonicalJsonTests** (10 tests): alphabetical key sorting, nested object sorting, root-level `signature`/`trace` exclusion, nested signature preservation, camelCase property names, array handling, null values, deterministic TransferToken output, field ordering verification.
  - **GalaSignerTests** (5 tests): signature population, deterministic signatures, different data produces different signatures, correct signature length (132 chars / 65 bytes), stable hash across identical inputs.
  - **PasswordCryptoServiceTests** (6 tests): mnemonic encrypt/decrypt round-trip, private key round-trip, wrong password throws, different ciphertext each time (random salt/nonce), record metadata correctness, custom iteration count.
- **Result**: 21 tests, all passing.
- **New files**: `Tests/Godot-Wallet.Tests.csproj`, `Tests/GalaCanonicalJsonTests.cs`, `Tests/GalaSignerTests.cs`, `Tests/PasswordCryptoServiceTests.cs`
- **Run with**: `dotnet test godot-wallet/Tests/`

### Break up GalaChainWallet.cs into smaller UI controllers
- **Problem**: `GalaChainWallet.cs` was 730 lines handling initialization, state display, wallet actions, and transfer logic all in one file.
- **Changes**:
  - Split into 3 partial class files using C# `partial class`:
    - **`GalaChainWallet.cs`** (~230 lines) — Fields, `Initialize`, `_Ready`, `_Process`, idle timer, `RefreshUi`, `RefreshBalances`, `Log`, `EnsureService`, `ShowUninitializedState`.
    - **`GalaChainWallet.WalletActions.cs`** (~210 lines) — Create, import, unlock, lock, copy address, mnemonic import, password dialog handling, balance refresh.
    - **`GalaChainWallet.Transfer.cs`** (~260 lines) — Transfer button/dialog, `RequestTransfer`, pending transfer stash/consume, dry-run preview, transfer validation/submission.
  - Removed dead code: `BuildStatusText()` method (was defined but never called).
  - Fixed the double-brace `Log` method body.
  - Also fixed `_importPrivateKeyInput`/`_passwordDialog` being on the same line in `_Ready` (formatting).
  - Added `<Compile Remove="Tests/**" />` to `Godot-Wallet.csproj` so the Godot build doesn't pick up test project files.
- **Files**: `Scripts/UI/GalaChainWallet.cs` (rewritten), `Scripts/UI/GalaChainWallet.WalletActions.cs` (new), `Scripts/UI/GalaChainWallet.Transfer.cs` (new), `Godot-Wallet.csproj`

### HTTP timeout configuration
- **Problem**: `HttpClient` used the .NET default of 100 seconds. Slow/unreachable servers would leave the UI showing "loading..." for nearly 2 minutes before failing.
- **Changes**:
  - Added `ReadTimeoutSeconds` (default 15) and `WriteTimeoutSeconds` (default 30) to `GalaChainNetworkConfig`.
  - `GalaChainClient`: `FetchBalancesAsync` and `DryRunTransferAsync` use the read timeout via `CancellationTokenSource`.
  - `GalaTransferClient`: `TransferAsync` uses the write timeout.
  - `TaskCanceledException` is caught and returned as `NetworkResult.TransportError` with a descriptive timeout message.
- **Files**: `GalaChainNetworkConfig.cs`, `GalaChainClient.cs`, `GalaTransferClient.cs`

### Refactor into Godot addon structure for distribution
- **Problem**: Wallet code lived in a flat `Scripts/` directory, tightly coupled to the demo project. Not distributable as a reusable Godot addon.
- **Changes**:
  - Moved all wallet source files (34 `.cs` + `.uid` files), the wallet scene, into `addons/galachain_wallet/`.
  - Structure: `addons/galachain_wallet/plugin.cfg`, `WalletPlugin.cs`, `scenes/GalaChainWallet.tscn`, `Scripts/Core/`, `Scripts/Models/`, `Scripts/UI/`.
  - Created `plugin.cfg` (name, description, author, version).
  - Created minimal `WalletPlugin.cs` extending `EditorPlugin`.
  - Updated `GalaChainWallet.tscn` script path: `res://addons/galachain_wallet/Scripts/UI/GalaChainWallet.cs`.
  - Updated `WalletFacade.cs` scene load: `res://addons/galachain_wallet/scenes/GalaChainWallet.tscn`.
  - Updated test project `.csproj` source file include paths.
  - Removed old empty `Scripts/` directory.
  - `WalletDemoGame.*` and `Tests/` remain outside the addon — they are not part of the distributable.
- **To use in another project**: Copy `addons/galachain_wallet/` into the target project's `addons/` directory. Add the Nethereum NuGet packages to the target's `.csproj`. Enable the plugin in Godot's Project Settings.

### Signature verification before submitting
- **Problem**: If signing produced a bad signature (corrupted state, key mismatch), the invalid transaction would be submitted to GalaChain and fail with a vague server error.
- **Change**: After signing, `GalaSigner.SignTransfer` now recovers the Ethereum address from the signature and verifies it matches the signer's key address. If they don't match, it throws `InvalidOperationException` with a clear message — the transaction is never submitted.
- **File**: `Scripts/Core/GalaSigner.cs`

### Remove dead code
- **Removed**: `ClipboardService.cs` (empty stub, unused), `WalletAddressModel.cs` (empty stub, unused), their `.uid` files, and the now-empty `Scripts/Services/` directory. `BuildStatusText()` was already removed in #16.
- **Updated**: `AGENTS.md` — removed `GalaWallet.Services` namespace entry.

### Testing feedback — low-hanging fruit batch (items #10, #9, #4, #5, #2)

1. **#10 — Balance refresh on mnemonic import** (bug fix): Added missing `await walletService.RefreshBalancesAsync()` in the `ImportMnemonic` case of `OnPasswordDialogConfirmed`. Balances now load immediately after importing a recovery phrase, matching the behavior of Create and Import Private Key.

2. **#9 — Enter key confirms dialogs**: Connected `TextSubmitted` signal on `_passwordInput`, `_importPrivateKeyInput`, `_importMnemonicInput`, and `_transferQuantityInput` to hide the dialog and call the corresponding confirmed handler. Pressing Enter while typing in any dialog input now confirms it.

3. **#4 — Display address as `eth|...`**: Added `FormatAsGalaAddress()` helper that converts `0x`-prefixed addresses to `eth|` format. Applied in `RefreshUi()` for the address display label and in `OnCopyAddressPressed()` for clipboard copy. Internal representation stays `0x` (Nethereum-native).

4. **#5 — Refresh Balances button near balances list**: Moved `RefreshBalancesButton` from the Wallet Info `ButtonsRow` to a new `BalancesHeaderRow` HBox inside `BalancesPanel`, alongside the "Balances" label. Button text shortened to "Refresh".

5. **#2 — Hide To/Quantity in game-initiated transfers**: Added `readOnly` parameter to `OpenTransferDialog`. When `true` (called via `RequestTransfer` from game code), the To label/input and Quantity label/input are hidden — the user only sees the summary and fee. When `false` (manual Transfer button), fields are visible and editable as before.

- **Files**: `GalaChainWallet.cs`, `GalaChainWallet.WalletActions.cs`, `GalaChainWallet.Transfer.cs`, `GalaChainWallet.tscn`

### Wallet events and demo balance display (feedback #1, #11)
- **Problem**: Game code had no way to react to wallet lifecycle events. Demo didn't show balances.
- **Changes**:
  - Added 7 C# events to `GalaChainWallet` (UI): `WalletCreated`, `WalletImported`, `WalletUnlocked`, `WalletLocked`, `TransferCompleted`, `TransferFailed`, `BalancesRefreshed`.
  - `WalletFacade` subscribes to UI events via `SubscribeToWalletEvents()` and re-exposes them for game code.
  - Added `GetBalances()` to `WalletFacade`.
  - Demo game subscribes to `BalancesRefreshed` and displays GALA balance in `GameBalanceLabel`.
  - Pattern documented in `AGENTS.md` for adding future events.
- **Files**: `GalaChainWallet.cs`, `GalaChainWallet.WalletActions.cs`, `GalaChainWallet.Transfer.cs`, `WalletFacade.cs`, `WalletDemoGame.cs`, `WalletDemoGame.tscn`, `AGENTS.md`, `INTEGRATION_GUIDE.md`

### Transfer response validation
- **Problem**: Transfer success was determined solely by HTTP status code. GalaChain can return HTTP 200 with `"Status": 0` (chain rejection) in the body.
- **Changes**:
  - Added `GalaChainResponse` model to parse the inner `Status` and `Message` fields.
  - `GalaTransferClient` now parses the response body and returns `Rejected` if `Status != 1`.
  - Extracted `ParseTransferResponse` as a static method for testability.
  - Added 6 unit tests covering success, rejection, error payloads, invalid JSON, empty objects, and null bodies.
- **Files**: `GalaTransferClient.cs`, `GalaChainResponse.cs` (new), `GalaTransferClientTests.cs` (new)
- **Test count**: 32 total, all passing.

### GDScript compatibility
- **Problem**: Plugin was C#-only. GDScript games couldn't use it.
- **Changes**:
  - Added `WalletBridge.cs` — Godot `Node` wrapper that exposes `WalletFacade` methods via GDScript-compatible types (Godot signals, `Array<Dictionary>` returns).
  - Updated `WalletPlugin.cs` to register `WalletBridge` as a `Wallet` autoload singleton on plugin enable.
  - Created `setup.sh` and `setup.ps1` scripts to automate NuGet dependency setup.
  - Added GDScript integration section to `INTEGRATION_GUIDE.md` with setup steps, examples, API reference, and troubleshooting.
  - Created `GDSCRIPT-COMPATIBILITY.md` with implementation notes.
- **New files**: `WalletBridge.cs`, `setup.sh`, `setup.ps1`

### Token icons in balance list (feedback #6)
- **Problem**: Balance list showed only text. No visual token identification.
- **Changes**:
  - Switched from `FetchBalances` to `FetchBalancesWithTokenMetadata` endpoint. Same request body, but response now includes token metadata (name, symbol, image URL, decimals).
  - Updated `GalaFetchBalancesResponse` to handle the `{ results: [{ balance, token }] }` paginated structure.
  - Added `GalaTokenMetadata` model (name, symbol, description, image, decimals, isNonFungible).
  - Added `ImageUrl` to `TokenBalanceModel`. Symbol now sourced from `token.symbol` metadata.
  - Balance list downloads token icons asynchronously via `HttpClient` with magic-byte format detection (PNG, JPG, WebP). Icons resized to 24x24 and cached in a static dictionary.
  - Retry logic (3 attempts, 500ms backoff) to handle intermittent TLS failures with some CDN domains in the Godot/.NET runtime.
  - Updated `WalletBridge` to include `image_url` in the GDScript balance dictionary.
- **Files**: `GalaFetchBalancesResponse.cs`, `GalaBalanceDto.cs`, `GalaChainNetworkConfig.cs`, `TokenBalanceModel.cs`, `GalaChainClient.cs`, `GalaChainWallet.cs`, `WalletBridge.cs`
- **Known issue**: .NET `HttpClient` has intermittent TLS handshake failures with `tokens.gala.games` and `static.gala.games` CDNs in the Godot runtime. Retries mitigate this but some icons may still fail to load on a given run. A future fix would be to use Godot's native `HttpRequest` node for icon downloads if the TLS issue can be resolved there.

## 2026-04-10

### Burn support
- **Problem**: Wallet only supported TransferToken. Players couldn't burn tokens they own.
- **Changes**:
  - New models: `GalaBurnTokensRequest` (DTO with `tokenInstances[]`, `uniqueKey`, `dtoExpiresAt`, `signature`), `BurnTokenQuantity` (wraps `tokenInstanceKey` + `quantity`), `BurnDraft` (UI-facing).
  - Added `BurnTokensUrl` computed property to `GalaChainNetworkConfig` (`/BurnTokens` endpoint).
  - `GalaSigner.SignBurn()` — refactored to share a private `SignPayload` helper with `SignTransfer`.
  - `BurnTokensPolicy` — validates quantity is positive and within available balance. Registered in `DtoPolicyRegistry`.
  - `IGalaTransferClient.BurnTokensAsync()` — new method. `GalaTransferClient` refactored to share a `PostSignedAsync` helper between transfer and burn.
  - `IWalletService.ValidateBurn()` and `SubmitBurnAsync()` — builds request, signs, submits, refreshes balances.
  - New UI partial class `GalaChainWallet.Burn.cs` mirrors the transfer flow: burn button, burn dialog, `RequestBurn`, pending burn stash/consume for locked-wallet case, summary preview.
  - New scene nodes: `BurnButton` (in Actions panel), `BurnDialog` with selected token label, quantity input, summary label.
  - Added `BurnCompleted(quantity, symbol)` and `BurnFailed(error)` events to `GalaChainWallet` and forwarded through `WalletFacade`.
  - Unlock flow now consumes pending burns (like pending transfers).
  - Demo: added "Burn 5 GALA" button that calls `_walletFacade.RequestBurn("5", "GALA")`, demonstrating game-initiated burn flow.
- **Scope note**: Burn is a player-signed operation — the player burns their own tokens with their own wallet key. This is in scope per the plugin's client-side boundary. Minting (which requires authority) remains out of scope.
- **Files**: `GalaBurnTokensRequest.cs` (new), `BurnDraft.cs` (new), `BurnTokensPolicy.cs` (new), `GalaChainWallet.Burn.cs` (new), `GalaChainNetworkConfig.cs`, `GalaSigner.cs`, `DtoPolicyRegistry.cs`, `IGalaTransferClient.cs`, `GalaTransferClient.cs`, `IWalletService.cs`, `WalletService.cs`, `WalletFacade.cs`, `GalaChainWallet.cs`, `GalaChainWallet.WalletActions.cs`, `GalaChainWallet.tscn`, `WalletDemoGame.cs`, `WalletDemoGame.tscn`

---

## Known issues remaining
See `PROJECT-ANALYSIS.md` Section 5 for the full critical issues list. Key items still open:
- ~~Mnemonic-imported wallets cannot sign after unlock (private key not re-derived)~~ — Fixed
- ~~No `dtoOperation` field on transfer requests~~ — **Deferred**: GalaChain's current Gateway API does not reliably support this field; operation routing is handled by endpoint URL. Revisit if GalaChain adds support.
- ~~`GalaCanonicalJson` does not exclude `signature`/`trace` fields~~ — Fixed
- ~~No DTO policy registry / allowlist enforcement~~ — Fixed
- ~~No idle timeout / auto-lock~~ — Fixed (5-minute timeout)
- ~~No unit tests~~ — Fixed (21 tests: serializer, signer, crypto)
- ~~Interface/class file name swap (`GalaTransferClient.cs` ↔ `IGalaTransferClient.cs`)~~ — Fixed
