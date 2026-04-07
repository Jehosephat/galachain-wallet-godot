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

---

## Known issues remaining
See `PROJECT-ANALYSIS.md` Section 5 for the full critical issues list. Key items still open:
- Mnemonic-imported wallets cannot sign after unlock (private key not re-derived)
- No `dtoOperation` field on transfer requests
- `GalaCanonicalJson` does not exclude `signature`/`trace` fields
- No DTO policy registry / allowlist enforcement
- No idle timeout / auto-lock
- No unit tests
- Interface/class file name swap (`GalaTransferClient.cs` ↔ `IGalaTransferClient.cs`)
