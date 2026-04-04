# GalaChain Wallet for Godot — PC MVP Technical Blueprint

Version: 1.0  
Date: 2026-04-04  
Target: Godot 4.5, C#/.NET, embedded in-game wallet UI, Windows-first desktop MVP

## 1. Purpose

Build a **small, opinionated GalaChain wallet module for Godot** that can be embedded directly inside a game and safely handle the minimum useful set of wallet behaviors:

- create a new Ethereum-style wallet
- import a wallet from private key
- restore from mnemonic phrase
- show the user’s address and allow easy copy
- fetch and display fungible token balances
- parse and confirm **only allowlisted GalaChain DTOs**
- sign and submit **TransferToken** transactions
- support a simple prototype game that exercises balance lookup and token transfer

This MVP is intentionally **not** a general-purpose wallet and **not** a blind signer.

---

## 2. Product decisions already made

### Confirmed direction
- **Core implementation:** C#/.NET inside Godot
- **Wallet UI:** embedded modal/scene inside the game
- **Signing model:** opinionated signer, not generic signer
- **First platform:** Windows desktop first, with abstractions to support Linux/macOS later
- **First signable operation:** `TransferToken`

### What this means
The wallet will be a **runtime module** and not a separate process, browser extension, or external app.  
The game calls a narrow wallet API. The wallet refuses unsupported DTOs.

---

## 3. Why this design fits GalaChain

GalaChain client-side auth uses **secp256k1 signatures with the same Ethereum-style signing primitives (keccak256 + secp256k1)**, supports DTO expiration via `dtoExpiresAt`, and recommends explicit `dtoOperation` values to prevent a DTO from being reused against the wrong operation. GalaChain DTO serialization is deterministic, and transfer DTOs are signed and then submitted to the Gateway API.  
References:
- https://docs.galachain.com/v2.7.0/authorization/
- https://docs.galachain.com/v2.7.0/integration-guide/
- https://docs.galachain.com/v2.7.0/chain-api-docs/functions/serialize/

Godot 4.5 supports C# on all desktop platforms, while Android and iOS C# exports exist but are still experimental. Godot also provides encrypted local file APIs, which are useful for an MVP keystore, though they are not a substitute for platform secure storage on mobile.
References:
- https://docs.godotengine.org/en/4.5/tutorials/scripting/c_sharp/
- https://docs.godotengine.org/en/4.5/classes/class_fileaccess.html

---

## 4. Core principles

1. **No blind signing**
   - The wallet never signs arbitrary raw JSON from gameplay code without validation.
   - Every supported DTO must have a parser, validator, and confirmation renderer.

2. **Allowlist only**
   - MVP supports a small number of DTOs.
   - Unknown methods are rejected.

3. **Human-readable intent must match the signed payload**
   - The confirmation UI is derived directly from the DTO fields that are actually signed.

4. **Short-lived transactions**
   - All submitted DTOs get a short `dtoExpiresAt`.

5. **Private key isolation**
   - Gameplay code never receives decrypted key material.
   - Signing happens inside the wallet service only.

6. **Simple enough to audit**
   - Keep the signing path narrow, deterministic, and heavily tested.

---

## 5. MVP scope

## Included in MVP
- Create wallet from mnemonic/private key seed flow
- Import wallet from private key
- Restore wallet from mnemonic phrase
- Display address and copy button
- Lock/unlock wallet with user passphrase
- Persist encrypted local keystore
- Fetch fungible token balances
- Parse, validate, preview, sign, and submit `TransferToken`
- Optional dry-run preview before submit
- Embedded wallet modal/scene for use inside a Godot game
- Demo game scene performing at least:
  - wallet create/import
  - balance fetch
  - transfer to another address

## Explicitly excluded from MVP
- NFT transfers
- burn support in shipping UI
- arbitrary DTO signing
- browser integration
- external wallet app / separate process
- multisig
- hardware wallet support
- mobile secure enclave / keystore support
- account switching between many wallets
- complex transaction history

---

## 6. Recommended platform target for MVP

### Phase 1 desktop target
**Windows-first desktop MVP**

Reason:
- The user is on Windows
- C# support on desktop is solid in Godot 4.5
- It reduces the secure storage and export matrix
- It allows the shortest path to a working prototype

### Cross-platform stance
Design the code so that storage and OS integration are abstracted behind interfaces:
- `IWalletStorage`
- `IClipboardService`
- `IClock`
- `INetworkClient`

That keeps Linux/macOS support open later without rebuilding the wallet domain model.

---

## 7. High-level architecture

```text
Game Code
   |
   v
WalletFacade (Godot-facing API)
   |
   +-- WalletSessionService
   +-- KeyManager
   +-- DtoPolicyRegistry
   +-- IntentRenderer
   +-- GalaChainClient
   +-- SecureStorage
   +-- AuditLogger
```

### Module responsibilities

#### WalletFacade
Public API exposed to the game.
- create wallet
- import wallet
- unlock/lock
- get address
- get balances
- preview transfer
- confirm transfer
- copy address

#### WalletSessionService
Owns unlocked state.
- tracks whether wallet is locked
- enforces idle timeout
- requires re-auth for high-risk actions
- clears sensitive in-memory material on lock

#### KeyManager
Creates, imports, derives, and signs.
- create mnemonic
- restore from mnemonic
- import private key
- derive address
- sign deterministic DTO payloads
- never expose raw private key to gameplay layer

#### SecureStorage
Stores encrypted keystore on disk.
- write keystore file
- read keystore file
- migrate file version if schema changes
- handles passphrase-based encryption for desktop MVP

#### DtoPolicyRegistry
Defines what may be signed.
- registry of supported DTO types
- method name / DTO schema mapping
- field validation rules
- refusal rules for unsupported or unsafe DTOs

#### IntentRenderer
Produces confirmation UI data from a DTO.
- token instance
- amount
- from
- to
- expiry
- operation name
- warnings
- raw payload view

#### GalaChainClient
HTTP client for Gateway / Explorer.
- fetch balances
- submit signed DTOs
- optional dry-run preview
- fetch token metadata where needed for display

#### AuditLogger
Low-risk event logs only.
- wallet created/imported
- wallet unlocked/locked
- signing requested
- transaction submitted / failed
- never logs mnemonic, private key, or full signed secrets

---

## 8. Project structure

```text
addons/
  galachain_wallet/
    plugin.cfg
    WalletPlugin.cs
    scenes/
      WalletPanel.tscn
      ConfirmTransferDialog.tscn
      CreateWalletDialog.tscn
      ImportWalletDialog.tscn
      UnlockDialog.tscn
    scripts/
      WalletFacade.cs
      WalletSignals.cs
      ui/
        WalletPanelController.cs
        ConfirmTransferController.cs
      domain/
        WalletState.cs
        WalletDescriptor.cs
        TransferIntent.cs
        BalanceItem.cs
      services/
        WalletSessionService.cs
        KeyManager.cs
        SecureStorage.cs
        GalaChainClient.cs
        ClipboardService.cs
      dtos/
        TransferTokenRequest.cs
        DryRunRequest.cs
      policy/
        DtoPolicyRegistry.cs
        ITransactionPolicy.cs
        TransferTokenPolicy.cs
      crypto/
        GalaDeterministicSerializer.cs
        Signer.cs
        MnemonicService.cs
      infra/
        HttpClientFactory.cs
        Json.cs
        Clock.cs
      tests/
        serializer/
        policy/
        signing/
        integration/
demo/
  GalaWalletDemo/
    scenes/
      DemoMain.tscn
    scripts/
      DemoMain.cs
```

---

## 9. Public API exposed to game code

The plugin should expose a deliberately narrow API.

### Example API surface

```csharp
public interface IWalletFacade
{
    bool HasWallet();
    bool IsLocked();

    Task<CreateWalletResult> CreateWalletAsync(string passphrase);
    Task<ImportWalletResult> ImportPrivateKeyAsync(string privateKeyHex, string passphrase);
    Task<RestoreWalletResult> RestoreMnemonicAsync(string mnemonic, string passphrase);

    Task UnlockAsync(string passphrase);
    void Lock();

    string GetAddress();
    string GetUserRef(); // returns eth|0x...
    Task<IReadOnlyList<BalanceItem>> GetBalancesAsync();

    Task<TransferPreviewResult> PreviewTransferAsync(
        string tokenInstance,
        string toEthAddress,
        string quantity
    );

    Task<SubmittedTxResult> SubmitTransferAsync(
        string tokenInstance,
        string toEthAddress,
        string quantity
    );

    bool CanHandleOperation(string dtoOperation);
}
```

### Rules for game-side callers
- Game code can request wallet actions.
- Game code cannot retrieve decrypted private key or mnemonic after creation/import.
- Game code should receive structured results, not raw crypto objects.

---

## 10. Wallet lifecycle

### Create wallet flow
1. Generate entropy
2. Build mnemonic
3. Derive private key and address
4. Show recovery phrase once
5. Ask user to confirm they saved it
6. Encrypt wallet data with local passphrase
7. Save encrypted keystore to disk
8. Unlock session in memory
9. Return address to game

### Import private key flow
1. Normalize hex input
2. Validate key length and curve compatibility
3. Derive address
4. Encrypt and store keystore
5. Unlock session

### Restore mnemonic flow
1. Validate mnemonic checksum and word count
2. Derive seed/private key/address
3. Encrypt and store keystore
4. Unlock session

### Lock flow
1. Clear in-memory private key/derived signing material
2. Mark session locked
3. Require passphrase for next signing attempt

### Auto-lock
- lock after configurable idle period, e.g. 5–10 minutes
- force unlock again for transaction submission even if wallet panel is open for long periods

---

## 11. Storage model

## Desktop MVP recommendation
Use a **local encrypted keystore file** protected by a user-chosen passphrase.

### Stored data
Recommended payload:

```json
{
  "version": 1,
  "walletType": "ethereum_secp256k1",
  "address": "0xabc...",
  "userRef": "eth|0xabc...",
  "ciphertext": "...",
  "kdf": {
    "name": "PBKDF2-or-Argon2",
    "salt": "...",
    "iterations": 210000
  },
  "cipher": {
    "name": "AES-256-GCM",
    "iv": "..."
  },
  "createdAt": "2026-04-04T00:00:00Z"
}
```

### Disk location
Use Godot `user://` application data path for the keystore file.

### Important note
Godot’s encrypted file APIs are useful, but the wallet should still treat passphrase-based keystore encryption as an explicit domain concern rather than hiding all security logic inside a generic file helper.

### What not to store
- plaintext mnemonic
- plaintext private key
- copied recovery phrase cache
- transaction payload cache containing secrets

---

## 12. Security model

## Threats this MVP tries to reduce
- accidental blind signing
- replayable DTOs
- gameplay scripts casually touching private keys
- users losing funds through misleading confirmation screens
- easy extraction of plaintext secrets from basic save files

## Threats this MVP does not fully solve
- compromised host OS
- malware/keyloggers on player machines
- memory scraping by privileged local attackers
- supply chain compromise in libraries
- mobile secure hardware integration
- hardware wallet-level isolation

### MVP hard rules
1. **Every submitted DTO gets `dtoExpiresAt`**
   - default window: 2–5 minutes

2. **Set `dtoOperation` explicitly**
   - do not rely on implied operation routing

3. **Generate a unique key**
   - every submit transaction gets a fresh `uniqueKey`

4. **Never sign unsupported DTOs**
   - unknown DTO => hard reject

5. **Never trust UI-only labels from the game**
   - derive approval text from the DTO itself

6. **Always show raw details on demand**
   - include advanced dropdown for exact payload fields

7. **Require explicit user action to sign**
   - no background auto-submit

8. **Separate preview from final submit**
   - the user should see what will be signed before it is signed

9. **Clear secrets on lock**
   - overwrite buffers where practical
   - drop references promptly

10. **No secrets in logs**
   - scrub error paths

---

## 13. DTO policy model

This is the heart of the opinionated wallet.

### Policy registry shape

```csharp
public interface ITransactionPolicy
{
    string OperationId { get; }
    bool CanParse(JsonElement dto);
    ValidationResult Validate(JsonElement dto);
    object ParseIntent(JsonElement dto);
    ConfirmationModel BuildConfirmation(object intent);
}
```

### MVP policy registry
- `asset-channel_basic-asset_GalaChainToken:TransferToken`

### Rejected in MVP
- anything else

### Why this matters
The wallet must not become a generic “JSON sign box.”  
It should behave more like:
- “I know how to safely sign TransferToken”
- “I refuse everything else”

---

## 14. TransferToken handling blueprint

GalaChain transfer docs show the standard pattern: construct DTO, set expiration, sign, submit.  
For MVP, the wallet should support exactly this operation end-to-end.

### Required fields to prepare for display and signing
- `from`
- `to`
- `tokenInstance`
- `quantity`
- `uniqueKey`
- `dtoExpiresAt`
- `dtoOperation`
- `signature` (filled only after user approves)

### Validation rules
- `from` must match unlocked wallet `eth|address`
- `to` must be valid user ref or valid raw Ethereum address that can be normalized into user ref
- `quantity` must be positive decimal string
- `tokenInstance` must be non-empty and pass formatting checks
- `dtoExpiresAt` must be within wallet-defined allowed max window
- `dtoOperation` must equal the exact supported operation id
- `signature` must not already be present before signing

### Confirmation UI must show
- action: Send token
- token instance key
- quantity
- sender
- recipient
- expiration time
- gateway endpoint / environment
- optional warnings:
  - recipient equals sender
  - quantity suspiciously large
  - expiration too long
  - unknown token metadata

### Recommended confirmation layout
- top summary card
- details list
- “advanced details” expandable JSON panel
- confirm button
- cancel button

---

## 15. GalaChain network client design

## Endpoints the wallet needs first
1. **Balances query**
2. **Transfer submit**
3. **Optional dry-run**
4. **Optional token metadata lookup**

### Environment configuration
The plugin should support environment config via a single object:

```json
{
  "name": "testnet",
  "gatewayBaseUrl": "https://...",
  "explorerApiBaseUrl": "https://...",
  "channel": "asset-channel",
  "chaincode": "basic-asset/GalaChainToken"
}
```

### Network rules
- explicit allowlist of environments
- no arbitrary runtime endpoint override from untrusted gameplay code
- timeout defaults
- retry only for safe read operations
- no retry on signed submit unless idempotency handling is clear

### Response handling
Normalize all network responses into a strict internal result model:
- `Success<T>`
- `Rejected`
- `TransportError`
- `ValidationError`

---

## 16. Deterministic serialization and signing

This is the most sensitive implementation area.

GalaChain’s docs explicitly say that manual signing must:
- serialize JSON deterministically
- sort fields alphabetically
- exclude top-level `signature` and `trace`
- convert BigNumber values to fixed-notation strings
- keccak256 hash the serialized payload
- sign with secp256k1 in Ethereum-style format  
References:
- https://docs.galachain.com/v1.9.3/authorization/
- https://docs.galachain.com/v2.7.0/chain-api-docs/functions/serialize/

## Design rule
Do **not** allow multiple ad hoc serializers.

Create one canonical serializer class:
- `GalaDeterministicSerializer`

That serializer should be:
- tiny
- tested with golden vectors
- used for every signature path
- version-locked in tests against known GalaChain examples

### Serializer requirements
- recursively sort object keys
- omit top-level `signature`
- omit top-level `trace`
- preserve exact intended value forms
- normalize numeric quantities into string form where GalaChain expects stringified values
- output UTF-8 JSON without whitespace

### Signer requirements
- accept canonical serialized string
- keccak256 hash the bytes
- sign using secp256k1
- emit GalaChain-compatible signature format
- optionally verify recovered signer address matches unlocked address before submit

### Practical recommendation
Use one mature Ethereum-compatible .NET crypto stack for:
- mnemonic handling
- address derivation
- keccak256
- secp256k1 signing

Do not mix multiple crypto libraries unless necessary.

---

## 17. Recommended transaction flow

### A. Game-driven simple transfer
1. Game requests `PreviewTransferAsync(token, to, quantity)`
2. Wallet validates input locally
3. Wallet builds unsigned DTO
4. Wallet attaches:
   - `from`
   - `uniqueKey`
   - `dtoExpiresAt`
   - `dtoOperation`
5. Wallet optionally calls dry-run
6. Wallet renders confirmation dialog
7. User confirms
8. Wallet signs DTO internally
9. Wallet submits DTO to Gateway
10. Wallet returns result to game
11. Wallet refreshes balances

### B. Advanced flow for future expansion
Game may pass a partially formed unsigned DTO, but:
- wallet must still normalize it
- wallet must still validate it against allowlist policy
- wallet must still override protected fields (`from`, `uniqueKey`, expiration, operation)
- wallet must still rebuild the approval UI from the final DTO actually being signed

---

## 18. UI blueprint

## Main wallet panel
Tabs:
- Account
- Balances
- Send
- Settings

### Account tab
- address
- copy button
- network name
- lock button
- restore/import access

### Balances tab
- fungible token list
- loading and refresh states
- optional search/filter

### Send tab
- token instance input/select
- quantity input
- recipient input
- preview button
- confirmation modal

### Settings tab
- auto-lock timeout
- export recovery phrase disabled by default in MVP unless explicitly re-authenticated
- wipe wallet / remove local keystore

## UX rules
- never display mnemonic after initial setup without strong re-auth
- always show exact operation name somewhere in advanced view
- always show environment
- use plain language first, raw DTO second

---

## 19. Demo game blueprint

Build a dead-simple prototype game scene that proves the wallet integration.

## Demo scene features
- Create Wallet button
- Import Wallet button
- Unlock Wallet button
- Address display + Copy button
- Refresh Balances button
- Token transfer form
- Status log panel

## Suggested flow
1. Launch demo
2. Create wallet
3. Show mnemonic and force acknowledge
4. Display address
5. Refresh balances from test environment
6. Enter recipient + quantity
7. Preview transfer
8. Confirm transfer
9. Show submit result
10. Refresh balances

This prototype is enough to validate:
- wallet lifecycle
- network configuration
- DTO parsing/preview
- signing correctness
- game integration ergonomics

---

## 20. Example domain models

```csharp
public sealed class WalletDescriptor
{
    public string Address { get; init; } = "";
    public string UserRef { get; init; } = "";
    public bool IsLocked { get; init; }
}

public sealed class BalanceItem
{
    public string TokenInstance { get; init; } = "";
    public string Quantity { get; init; } = "";
    public string? Symbol { get; init; }
    public string? DisplayName { get; init; }
}

public sealed class TransferIntent
{
    public string OperationId { get; init; } = "";
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string TokenInstance { get; init; } = "";
    public string Quantity { get; init; } = "";
    public string UniqueKey { get; init; } = "";
    public long DtoExpiresAt { get; init; }
}

public sealed class ConfirmationModel
{
    public string Title { get; init; } = "";
    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>();
    public string RawPayloadJson { get; init; } = "";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
```

---

## 21. Testing strategy

## Unit tests
### Serializer tests
- keys sorted alphabetically
- excludes `signature` and `trace`
- stable output for nested objects
- numeric string normalization
- golden vector tests

### Key tests
- mnemonic generation valid
- restore from mnemonic produces same address
- private key import derives correct address

### Policy tests
- valid `TransferToken` passes
- unsupported operation rejected
- mismatched `from` rejected
- malformed quantity rejected
- pre-signed DTO rejected

### Signing tests
- signature verification round-trip
- recovered address matches wallet address
- same DTO => same serialized payload

## Integration tests
- mock Gateway balance fetch
- mock transfer submit
- preview -> confirm -> sign -> submit flow
- lock/unlock lifecycle

## Manual QA
- create/import/restore
- wrong passphrase
- expired DTO
- offline transport errors
- duplicate submit prevention behavior
- address copy behavior

---

## 22. Audit checklist

Before anyone calls this “secure,” run through this list:

- [ ] single canonical serializer
- [ ] deterministic signing tests pass
- [ ] no generic arbitrary-sign API exposed
- [ ] protected fields overridden by wallet, not game
- [ ] no plaintext secrets on disk
- [ ] secrets cleared on lock
- [ ] logs scrubbed
- [ ] allowlisted endpoints only
- [ ] approval UI derived from signed DTO
- [ ] unsupported DTOs rejected
- [ ] expiration enforced
- [ ] unique key generated per submit
- [ ] confirmation required before signing
- [ ] code reviewed specifically for:
  - serializer mismatches
  - passphrase handling
  - race conditions on lock/unlock
  - accidental exception logging of sensitive values

---

## 23. Implementation plan

## Milestone 1 — Skeleton
- Godot plugin scaffold
- wallet panel scene
- WalletFacade interface
- environment config
- basic status and logging

## Milestone 2 — Key lifecycle
- create wallet
- import private key
- restore mnemonic
- encrypted keystore persistence
- lock/unlock session

## Milestone 3 — Read path
- get address
- copy address
- fetch fungible balances
- render balances UI

## Milestone 4 — Transfer path
- TransferToken DTO builder
- deterministic serializer
- signer
- transfer confirmation dialog
- submit to Gateway

## Milestone 5 — Safety hardening
- timeout / auto-lock
- dry-run support
- raw DTO advanced view
- warnings / validation improvements
- scrub logs

## Milestone 6 — Demo game
- simple scene
- faucet/setup notes for test environment
- end-to-end proof of transfer

---

## 24. Suggested first backlog

1. Scaffold Godot addon
2. Implement wallet state model
3. Implement passphrase-based keystore
4. Implement mnemonic + import logic
5. Implement address derivation and display
6. Implement Gateway read client for balances
7. Implement canonical serializer
8. Implement signing and verification tests
9. Implement TransferToken policy + confirmation UI
10. Implement submit path
11. Build demo scene
12. Run security review pass

---

## 25. Post-MVP path

### High-value next steps
1. **Burn support**
   - only after TransferToken is solid

2. **NFT read support**
   - fetch token instances / NFT display
   - no transfer yet

3. **NFT transfer support**
   - add another explicit policy type

4. **Windows secret-store integration**
   - add an OS-backed key-protection path if desired

5. **Android support**
   - use Godot Android plugin path + Android Keystore
   - note Godot’s Android plugin system is documented and C# Android export remains experimental in 4.5

6. **iOS support**
   - Keychain/Secure Enclave path
   - also currently experimental for C# export

### What should still stay out for a while
- arbitrary DTO signing
- external wallet bridging
- browser extension workflows
- multisig
- hardware wallets

---

## 26. Final recommendation

The right MVP is **small, opinionated, and boring**.

Do not chase “full wallet” status yet.  
Ship a wallet that can do these four things correctly:
- create/import/restore
- display address
- display balances
- preview/sign/submit `TransferToken`

If those four behaviors are clean, reliable, and easy to audit, then the wallet is worth extending.

If they are not, adding burns, NFTs, mobile, or generic DTO handling will only magnify the risk.

---

## 27. Source references

- GalaChain authorization and signing:
  - https://docs.galachain.com/v2.7.0/authorization/
  - https://docs.galachain.com/v1.9.3/authorization/

- GalaChain integration guide:
  - https://docs.galachain.com/v2.7.0/integration-guide/

- GalaChain deterministic serialization:
  - https://docs.galachain.com/v2.7.0/chain-api-docs/functions/serialize/

- GalaChain DTO references:
  - https://docs.galachain.com/v2.0.0/chain-api-docs/classes/TransferTokenDto/
  - https://docs.galachain.com/v2.7.0/chain-api-docs/classes/DryRunResultDto/
  - https://docs.galachain.com/v2.7.0/chain-api-docs/classes/FetchTokenInstancesDto/
  - https://docs.galachain.com/v1.9.0/chaincode-docs/functions/fetchBalances/

- Godot C# support:
  - https://docs.godotengine.org/en/4.5/tutorials/scripting/c_sharp/

- Godot encrypted/local file storage:
  - https://docs.godotengine.org/en/4.5/classes/class_fileaccess.html

- Godot Android plugin path for later expansion:
  - https://docs.godotengine.org/en/4.5/tutorials/platform/android/android_plugin.html
