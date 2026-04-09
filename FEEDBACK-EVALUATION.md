# Testing Feedback Evaluation — 2026-04-09

Source: `testing-feedback-4-9-26.md`

---

## 1. Demo should display user balance from the wallet — RESOLVED

**Feedback**: The demo game should show the player's balance, not just the wallet address.

**Approach**: `WalletFacade` already exposes `RefreshBalancesAsync()` but doesn't expose `GetBalances()`. Add a `GetBalances()` passthrough on `WalletFacade` that returns the current balance list. The demo game can then display a summary (e.g., "GALA: 951.08") in the `GameStatusLabel` or a new label.

**Changes needed**:
- `WalletFacade`: add `List<TokenBalanceModel> GetBalances()` delegating to `_walletService.GetBalances()`
- `WalletDemoGame.cs`: after opening/unlocking, call `GetBalances()` and display in a label
- `WalletDemoGame.tscn`: add a balance display label below the status

**Scope**: Small. ~20 lines of code.

---

## 2. Game-initiated transfer dialog should hide To/Quantity fields — RESOLVED

**Feedback**: When the game calls `RequestTransfer` with pre-filled values, the To and Quantity fields should not be editable (or not visible). The user should just see the preview and confirm.

**Approach**: Add a `bool` flag to `OpenTransferDialog` indicating whether it was game-initiated. When true, hide `TransferToInput`, `TransferToLabel`, `TransferQuantityInput`, `TransferQuantityLabel` and only show the summary/preview. When the user opens the transfer dialog manually (via the Transfer button), the fields remain editable as they are now.

**Changes needed**:
- `GalaChainWallet.Transfer.cs`: `OpenTransferDialog` takes an optional `bool readOnly` parameter. When true, set the input fields to `Visible = false` (or `Editable = false`) and their labels too.
- Reset visibility back to true when the dialog closes or when opened manually.

**Scope**: Small-medium. ~15 lines in Transfer.cs, no new files.

---

## 3. General visual cleanup (alignment, margins, backgrounds, colors) — OPEN

**Feedback**: The wallet UI needs polish — better alignment, margins, backgrounds, colors.

**Approach**: This is best done iteratively in the Godot editor rather than by editing `.tscn` files in code. Key areas to address:

- **Title**: Larger font, bold, maybe a colored header bar.
- **Section labels** ("Wallet", "Actions", "Balances", "Log"): Consistent styling, maybe bold or slightly larger.
- **PanelContainers**: Add inner margins so content isn't flush against edges.
- **Buttons**: Consistent width (stretch to fill), maybe minimum height for touch targets.
- **Balance list**: Alternate row coloring or subtle separators.
- **Log panel**: Slightly dimmer text or monospace font to distinguish from primary content.
- **Overall**: Consider a Godot Theme resource (`.tres`) applied to the wallet root node for consistent colors/fonts across all elements.

**Recommendation**: Create a `wallet_theme.tres` Theme resource in the addon's `scenes/` directory. Define styles for Panel, Button, Label, LineEdit, ItemList, RichTextLabel. Apply it to the root `GalaChainWallet` node. This way the entire wallet gets consistent styling from one file, and games can override it with their own theme.

**Scope**: Medium. Mostly editor work + theme resource creation. No logic changes.

---

## 4. Address field should display `eth|...` instead of `0x...` — RESOLVED

**Feedback**: The wallet address is shown as `0xD1499B10A0e1F4912FD1d771b183DfDfBDF766DC` but should display as `eth|D1499B10A0e1F4912FD1d771b183DfDfBDF766DC` to match GalaChain conventions.

**Approach**: Two options:

**Option A — Display-only conversion**: Change `RefreshUi()` in `GalaChainWallet.cs` to convert the displayed address. The stored address stays `0x`-prefixed internally (as Nethereum returns it), but the UI displays the `eth|` form. The Copy Address button should also copy the `eth|` form.

**Option B — Store as `eth|` format**: Change `WalletService` to store the `eth|` form in `WalletState.Address`. This would require updating `GalaChainClient.BuildOwnerAlias()` and `WalletService.ToGalaAlias()` since they currently do the conversion.

**Recommendation**: Option A is safer — keep the internal representation as `0x` (Nethereum-native) and convert at the display/copy boundary only. This avoids breaking the signing path or balance fetch.

**Changes needed**:
- `GalaChainWallet.cs`: in `RefreshUi()`, convert `_walletService.GetAddress()` to `eth|` form before setting `_addressValueLabel.Text`
- `GalaChainWallet.WalletActions.cs`: in `OnCopyAddressPressed()`, convert to `eth|` form before clipboard copy
- Add a small helper method `FormatAsGalaAddress(string address)` to do the conversion

**Scope**: Small. ~10 lines.

---

## 5. Refresh Balances button should be near the balances list — RESOLVED

**Feedback**: The Refresh Balances button is currently in the Wallet Info section (alongside Copy Address). It should be near the Balances panel where it's contextually relevant.

**Approach**: Move the `RefreshBalancesButton` node in `GalaChainWallet.tscn` from `WalletInfo/VBoxContainer/ButtonsRow` to `BalancesPanel/VBoxContainer`, either above or below the `BalancesList`. A horizontal row with "Balances" label + refresh button would work well.

**Changes needed**:
- `GalaChainWallet.tscn`: move the button node to a new parent, or create an HBoxContainer in BalancesPanel for the label + button row.
- No code changes — the button is referenced by unique name `%RefreshBalancesButton`, so it works regardless of where it sits in the tree.

**Scope**: Small. Scene edit only.

---

## 6. Balances could include token icon (use FetchBalancesWithTokenMetadata) — RESOLVED

**Feedback**: Show token icons alongside balances. GalaChain has a `FetchBalancesWithTokenMetadata` endpoint that returns image URLs.

**Approach**: Replace (or supplement) the current `FetchBalances` call with `FetchBalancesWithTokenMetadata`. This endpoint returns the same balance data plus token metadata including `image` (a URL to the token icon). Then use Godot's `HTTPRequest` or `HttpClient` to download the image, create a `Texture2D`, and display it in the balance list.

**Changes needed**:
- `GalaChainNetworkConfig`: add `FetchBalancesWithTokenMetadataUrl` property
- `GalaBalanceDto`: add metadata fields (`name`, `symbol`, `image`, `decimals`, etc.) or create a new response DTO
- `GalaChainClient.FetchBalancesAsync`: switch to the new endpoint
- `TokenBalanceModel`: add `ImageUrl` field
- `GalaChainWallet.cs` `RefreshBalances()`: the `ItemList` supports icons — use `AddItem(text, icon)`. Download and cache token icons as textures.
- Consider an icon cache so images aren't re-downloaded every refresh.

**Scope**: Medium-large. New endpoint, response model changes, async image downloading and caching, ItemList icon integration. ~100-150 lines across several files.

**Risk**: Token icon URLs may be external (e.g., `https://static.gala.games/...`). Need to handle slow/missing images gracefully — show a placeholder while loading, skip if download fails.

---

## 7. Demo expanded to mint an NFT to the user — DEFERRED

**Feedback**: Could the demo mint an NFT to the user? Is minting a wallet function?

**Analysis**: Minting is a different GalaChain operation (`MintToken`) from transferring. In the current architecture, adding minting would require:

1. A new `ITransactionPolicy` implementation for `MintToken`
2. A new request DTO (`GalaMintTokenRequest`)
3. A new method on `GalaTransferClient` (or a new `GalaMintClient`)
4. A new signer payload for the mint operation
5. UI for the mint confirmation dialog
6. The demo would need minting authority — typically only authorized addresses can mint

**Recommendation**: This is a significant feature addition, not a quick demo tweak. It would be the first operation beyond `TransferToken`, which would validate the policy registry and extensibility model. However, minting requires the sender to have minting authority on the token class, which is a chain-side permission — the demo wallet likely doesn't have this.

**Alternative**: The demo could display an NFT that was transferred to the user rather than minting one. Or the demo could call a game server API that mints on behalf of the user (the wallet wouldn't be involved in that flow).

**Scope**: Large. New operation end-to-end. Defer until the base wallet is polished.

---

## 8. Seed phrase should be easier to read and copy (copy button) — RESOLVED

**Feedback**: The recovery phrase dialog just dumps the words into an AcceptDialog. It should be more readable and have a copy button.

**Approach**: Replace the `AcceptDialog` for the recovery phrase with a custom `ConfirmationDialog` (or a new scene) that:
- Displays words in a grid (3 columns x 4 rows for 12 words), each numbered
- Has a "Copy to Clipboard" button
- Has a "I've saved my recovery phrase" confirmation button
- Uses a monospace or larger font for readability

**Changes needed**:
- `GalaChainWallet.tscn`: add a new `ConfirmationDialog` for recovery phrase display with a GridContainer and a copy button
- `GalaChainWallet.WalletActions.cs`: replace the `_simpleMessageDialog` usage for recovery phrase with the new dialog. Wire up the copy button.
- The copy button calls `DisplayServer.ClipboardSet(phrase)`.

**Scope**: Medium. New dialog layout + wiring. ~30-40 lines of code + scene edits.

---

## 9. Enter key = OK, Escape = Cancel — RESOLVED

**Feedback**: Pressing Enter should confirm dialogs, Escape should cancel.

**Analysis**: Godot's `ConfirmationDialog` and `AcceptDialog` already handle Enter/Escape by default — the OK button has focus and Enter confirms it, Escape closes the dialog. The issue is likely that after the dialog opens, focus is set to a `LineEdit` (e.g., password input), and Enter submits the text rather than confirming the dialog.

**Approach**: Connect the `text_submitted` signal on each `LineEdit` inside dialogs to the dialog's confirmation action. This way pressing Enter while typing in the input field confirms the dialog.

**Changes needed**:
- `GalaChainWallet.cs` `_Ready()`: connect `_passwordInput.TextSubmitted` to trigger `_passwordDialog` confirmation
- Same for `_importPrivateKeyInput`, `_importMnemonicInput`, `_transferToInput`/`_transferQuantityInput`
- For transfer inputs, Enter on the last field (quantity) should confirm; or Enter on any field confirms.

**Scope**: Small. ~5-10 lines of signal connections.

---

## 10. Balances refresh upon importing mnemonic — RESOLVED

**Feedback**: After importing a mnemonic, balances aren't fetched — the user has to manually click Refresh.

**Analysis**: Looking at the code in `OnPasswordDialogConfirmed`, the `ImportMnemonic` case doesn't call `RefreshBalancesAsync()`:

```csharp
case PendingPasswordAction.ImportMnemonic:
    walletService.ImportMnemonic(_pendingMnemonic, password);
    _pendingMnemonic = "";
    RefreshUi();  // <-- no balance fetch
    Log("Imported wallet from recovery phrase.");
    break;
```

The `CreateWallet` and `ImportWallet` cases both call `await walletService.RefreshBalancesAsync()` before `RefreshUi()`. The mnemonic import case was simply missed.

**Changes needed**:
- `GalaChainWallet.WalletActions.cs`: add `await walletService.RefreshBalancesAsync();` before `RefreshUi()` in the `ImportMnemonic` case.

**Scope**: Trivial. 1 line.

---

## 11. Signals from wallet for demo to consume — RESOLVED

**Feedback**: The demo should be able to react to wallet events (wallet created, unlocked, transfer completed, etc.) via Godot signals.

**Approach**: Add C# events or Godot signals to `WalletFacade` that fire on key lifecycle events. Games subscribe to these to update their UI or trigger game logic.

Proposed signals:
- `WalletCreated(string address)` — after wallet creation
- `WalletUnlocked(string address)` — after successful unlock
- `WalletLocked()` — after lock (manual or auto-lock)
- `TransferCompleted(string toAddress, string quantity, string symbol)` — after successful transfer
- `TransferFailed(string error)` — after failed transfer
- `BalancesRefreshed()` — after balances are updated

**Implementation options**:

**Option A — C# events on WalletFacade**: Simple, no Godot dependency. `wallet.OnTransferCompleted += (to, qty, sym) => { ... };`. Works well for C# game code.

**Option B — Godot signals on GalaChainWallet node**: Uses `[Signal]` delegates. Games connect via `wallet.Connect("TransferCompleted", ...)`. More idiomatic for Godot but requires access to the wallet node.

**Option C — Both**: Facade fires C# events; the UI node emits Godot signals. Facade events are for game code, signals are for scene-based wiring.

**Recommendation**: Option A (C# events on WalletFacade) for now. It's the simplest, matches how the demo already uses the facade, and doesn't require the game to know about the wallet's internal node structure. Can add Godot signals later if needed.

**Changes needed**:
- `WalletFacade`: add `event Action<string>? WalletCreated`, etc.
- `GalaChainWallet.WalletActions.cs` / `GalaChainWallet.Transfer.cs`: after key operations, call through to facade events (or the facade wraps the service calls and fires events after them)
- `WalletDemoGame.cs`: subscribe to events and update display

**Scope**: Medium. ~40-60 lines across facade, UI, and demo.

---

## Implementation Priority

| # | Item | Effort | Impact | Status |
|---|------|--------|--------|--------|
| 10 | Balance refresh on mnemonic import | Trivial | High (bug) | **Resolved** |
| 9 | Enter key = OK in dialogs | Small | High (UX) | **Resolved** |
| 4 | Display address as `eth\|...` | Small | Medium (consistency) | **Resolved** |
| 5 | Move Refresh Balances button | Small | Medium (UX) | **Resolved** |
| 2 | Hide To/Qty in game-initiated transfer | Small | Medium (UX) | **Resolved** |
| 1 | Demo shows balance | Small | Medium (demo) | **Resolved** |
| 8 | Better recovery phrase dialog | Medium | Medium (UX) | **Resolved** |
| 11 | Wallet signals for game events | Medium | Medium (integration) | **Resolved** |
| 3 | Visual cleanup / theming | Medium | Medium (polish) | Open |
| 6 | Token icons in balances | Large | Low (nice-to-have) | **Resolved** |
| 7 | NFT minting in demo | Large | Low (new feature) | Deferred |
