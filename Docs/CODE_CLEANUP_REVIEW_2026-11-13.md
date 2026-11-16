# Storm Gambit code cleanup review

Date: November 13, 2026

## Scope and confidence

Inventoried 96 C# files under `Assets/Scripts` and `Assets/ScriptableObjects/Mages`. Reviewed the main card, mage, turn, board, AI, networking, resurrection, promotion, and victory flows. Cross-checked script GUIDs against `.unity`, `.prefab`, and `.asset` files throughout Assets, and searched code references and serialized button callbacks. Reviewed the installed Netcode package implementation for RPC attribute handling.

This is a static review, not a Unity compilation, Play Mode test, device test, or proof that every execution path is correct. No source, scene, prefab, package, or Git configuration changes were made. This report is the only addition.

“Removal candidate” below means no current code/serialized dependency was found, not an instruction to delete blindly. Remove scripts with their `.meta` files through Unity, preserve GUIDs for retained assets, and check for missing references afterward. Existing uncommitted victory/rematch work was preserved.

## Fix behavior before cosmetic cleanup

### 1. P1 — Spell count does not represent the hand

Evidence:

- `Assets/Scripts/Cards/CardDrawer.cs:35`: starting cards are instantiated without incrementing `CurrentSpellCount`.
- Same file, lines 81 and 84: `DrawOneSpellCard()` increments the count twice for one card.
- `Assets/Scripts/Cards/CardUI.cs:36`: `ConsumeCardAfterSuccessfulCast()` destroys the card without notifying the drawer.
- Same file, line 311: the decrement exists only in the fallback path after the five recognized spell UI branches have already returned.
- `Assets/Scripts/Chess Game/Rules/StormEndgameEvaluator.cs:90`: checkmate decisions use this count, or its synchronized network counterpart.

Consequence: a hand can be reported as empty when spells remain, or retain a positive count after spells are gone. This can cause incorrect wins/losses or prevent a match from ending.

Cleanup: make one component own hand membership and count. Use one add operation per drawn spell and one remove operation per confirmed successful cast. Cancellation must preserve the hand. Avoid independently incrementing the same resource in both `NetPlayer.AddSpellServer()` and client count synchronization without a clear authoritative contract.

### 2. P1 — Solo card drawing also runs in multiplayer

Evidence:

- `Assets/Scripts/Cards/SoloCardDrawController.cs:15`: subscribes to `TurnManager.OnTurnChanged` without checking solo mode or network activity.
- `Assets/Scenes/GameScene.unity:1457`: component is enabled on the active `SoloCardDrawer` GameObject.
- Scene line 1461: serialized draw interval is **7**, overriding the script default of 10.
- `Assets/Scripts/Network/GameStat.cs:202`: multiplayer separately draws on multiples of 10.

Consequence: multiplayer receives an additional draw schedule through the supposedly solo controller. The scene and code also disagree about the configured interval.

Cleanup: select a single draw owner per game mode. Put the interval in a shared rules/configuration asset, or explicitly document intentionally different intervals. The counter starts at full-move number 1, so also decide whether the rule means entering move N or completing N full moves.

### 3. P1 — Rejected network moves can still advance counters and award cards

Evidence: `Assets/Scripts/Network/GameStat.cs:189` updates `MoveNumber` and potentially draws cards before bounds, movement legality, and protected-target validation at lines 216 onward. A legal-turn request with an invalid destination can therefore change match bookkeeping before it is rejected.

Cleanup: validate first; commit move, counters, resource grants, and turn transition only after acceptance. Decide how promotion and spell-ended turns contribute to the counter.

### 4. P1 — Server validation is inconsistent across actions

Evidence:

- `Assets/Scripts/Network/GameStat.cs:135`: move requests validate the sender's turn but do not compare the selected piece's team with the sender's side. Local input performs that check, but the server boundary does not.
- Same file, line 618: Freeze checks only whether the piece exists; sender/turn/resource checks are absent.
- Same file, line 1045: Divine Protection has the same gap, including no server check that the target belongs to the caster.
- Same file, line 917: Teleport does not use the supplied sender parameters to establish the caster's side/turn.
- Most action entry points lack a common game-over and pending-promotion guard.

Consequence: stale, repeated, or directly issued RPC requests can modify state that normal local UI would reject. This is an authority-boundary issue, not simply old syntax.

Cleanup: centralize validation of sender, side, turn, match phase, target, and available spell/mage resource. Keep spell-specific target rules explicit. A successful local click is not evidence of server acceptance.

### 5. P2 — Spell UIs consume resources before server acceptance

Evidence: `ResurrectionSpellUI.cs:42` and `TeleportationSpellUI.cs:113` send an RPC and immediately call their success/consume path. Server rejection has no matching restoration/response path. The Resurrection network branch also bypasses the offline branch's `RegisterFreeSpellCast()` call.

Cleanup: distinguish pending, accepted, rejected, and cancelled actions. Consume the card or mage ability only after an authoritative success response. Centralize per-turn spell use instead of distributing it among individual UIs.

### 6. P2 — Rejected movement has no rejection response

Evidence:

- `Assets/Scripts/Chess Game/ChessPiece.cs:678`: visually places the piece at the requested destination and sets `awaitingNetworkMove = true`.
- `GameStat.cs:135`: invalid moves return without a rejection RPC.
- `ChessPiece.cs:1199`: `RejectNetworkMove()` exists but has no caller.

Consequence: a rejected move can leave the piece displayed at the wrong location and waiting for confirmation until another accepted action changes the turn.

Cleanup: send an explicit rejection to the requesting client and restore its piece from the authoritative cell.

### 7. P2 — AI rejection can leave the match waiting indefinitely

Evidence: `Assets/Scripts/AI/ComputerPlayer.cs:261` logs a rejected move but does not choose another move, invoke a fallback, or transition the turn. The `finally` block only clears `isThinking`; another turn-change event is not generated. `CanAIActNow()` also does not check network activity, while `TryExecuteAIMove()` blocks it later.

Cleanup: define recovery for a Stockfish move rejected by Storm status rules; make mode gating explicit at the AI entry point. This overlaps the already-deferred Freeze/Protection AI work and should retain that product context.

## High-confidence obsolete or dormant candidates

These files had no serialized GUID references and no active external code references, apart from the dormant chains described here.

| Candidate | Reason / recommended disposition |
| --- | --- |
| `Assets/Scripts/Network/NetBoardSync.cs` | Empty NetworkBehaviour. Remove if there is no planned implementation. |
| `Assets/Scripts/Network/ClientReconciler.cs` | Empty Start/Update placeholder. There is no implemented client reconciler here. |
| `Assets/Scripts/Network/BoardStateDTO.cs` | PieceDTO/BoardSnapshot have no consumers outside their own file. Retain only with an explicit snapshot-sync plan. |
| `Assets/Scripts/Spells Scripts and prefabs/SpellManager.cs` | Unused alternative dispatch path; Resurrection is still a log-only stub and Canvas lookup differs from the active path. |
| `Assets/Scripts/Spells Scripts and prefabs/SpellButton.cs` | Entire proposed implementation is commented out. |
| `Assets/Scripts/Spells Scripts and prefabs/SpellData.cs` | No assets use it; its only external reference is commented code in SpellButton. Active cards use CardData. |
| `Assets/Scripts/Spells Scripts and prefabs/ResurrectionSpell.cs` and `ISpell.cs` | Unused prototype. Chooses the first captured ChessPiece rather than using the current graveyard-data UI flow. ISpell is used only by this prototype. |
| `Assets/Scripts/Spells Scripts and prefabs/Resurrection/ResurrectionPanel.cs` | Unused alternative resurrection UI with different turn-ending behavior. Active path is ResurrectionSpellUI. |
| `Assets/Scripts/UI/ButtonHoverEffect.cs` | Empty script actually declares global `Button`, not ButtonHoverEffect. No script GUID references found. Its type shadows UnityEngine.UI.Button and encourages fully qualified names elsewhere. Audit unqualified Button fields while removing this type. |
| `Assets/Scripts/Characters/CharacterPanelHUD.cs` | No serialized references or callers. Its only special path calls CardUI.LoadMage. |
| `Assets/ScriptableObjects/Mages/GameSession.cs`, `MageDatabase.cs` | No active callers or serialized assets. |
| `Assets/ScriptableObjects/Mages/MageData.cs`, `MageAbilityData.cs`, `MageIDs.cs` | Dormant alternative mage model, linked to CharacterPanelHUD/CardUI.LoadMage rather than active CardData-based selection. Retire together or finish a deliberate migration. |

Remove `CardUI.LoadMage`, its MageData callback, and related fields only after deciding the fate of this entire dormant mage chain. Do not delete individual types while leaving their callers behind.

## Old systems that still have serialized references

- **GameBoot** (`Assets/Scripts/GameBoot/GameBoot.cs:10`): still attached to BoardManager in GameScene. Start only logs; the selection call is commented out, its private callback has no caller, and SelectedCharacter has no readers. Detach the component before removing the script. Do not delete BoardManager, which hosts other active components.
- **CharacterSelectionUI**: referenced by `Assets/Scripts/Characters/CharcterUIPanel.prefab`. Active selection also has CharacterChoicePanel. Treat the former as a legacy-prefab candidate; establish that the prefab is no longer needed before deletion.
- **NetworkUI**: still referenced by host/client/shutdown button callbacks in GameScene, on an initially inactive NetworkPanel. It is a LAN/debug UI alongside Relay, not an unreferenced script.
- **NetGameBoot**: referenced by GameScene, the recovery scene, and `Assets/Scripts/Network/Network Manager.prefab`. The GameScene component is disabled. Contains an immediate-mode LAN UI, an old IP address, and commented-out GameState spawning. Isolate in a development scene or explicitly retire its references.
- **Readme.cs / Editor/ReadmeEditor.cs / Assets/Readme.asset**: starter-project documentation chain. Optional removal together; not gameplay cleanup.
- **Debug tools**: StockfishAITest, StockfishNoMoveDebugTester, CheckDebugTester, MoveValidationDebugTester, and PositionSimulationDebugTester are attached to GameScene. StockfishAITest automatic startup is false. These are mostly manually invoked diagnostics, not proof of a runtime bug. Move them to an intentional development workflow. Reuse UciMoveParser rather than retaining a second parser inside StockfishAITest.
- **KingSlaySequenceController**: keep it. It implements the actual cinematic; the K replay input is already inside `#if UNITY_EDITOR`. GameScene overrides debugPreviewOnly to false. Do not mistake it for a disposable tester.

## Refactoring and maintenance backlog

1. **One game-end presentation path.** `ChessBoard.cs:441`, `GameStat.cs:1396`, `StormEndgameEvaluator.cs`, and the AI resolver all participate in result presentation. King capture on the host invokes ShowVictoryClientRpc and then also runs the board's local victory/audio block, allowing duplicate presentation/audio. Let one match-result operation feed VictoryPanelUI for all modes.
2. **Separate responsibilities gradually.** GameStat.cs is 1,746 lines, ChessPiece.cs 1,219, and ChessBoard.cs 728 at review time. Extract resource state, action validation, move resolution, spell resolution, and presentation in small steps. ChessPiece currently makes tap and AI movement reuse OnMouseUp; a move operation should be callable independently of pointer callbacks.
3. **Remove duplicate/dead state.** ChessBoard._byId is never used. capturedPieces and its GetCapturedPieces/HasCapturedPieces/RemoveCapturedPiece overload form the old resurrection list, while active captures populate Graveyard. CardDrawer.cardPool is superseded by defaultCardPool/stockfishCampaignPool. ChessPiece.PieceId/Team/BoardCell are unused NetworkVariables alongside active Id/team/currentCell. NetworkVariable removal changes the network schema: rebuild both endpoints together and verify compatibility.
4. **Unify turn bookkeeping.** TurnManager, GameState, SoloCardDrawController, and TurnCounterOffline all track overlapping notions of turns or moves. TurnCounterOffline also has no mode guard. `HasAnyMovablePiece()` tests only whether an unfrozen/unstunned/unprotected piece exists, not whether it has a valid destination; its name overstates its contract. The Freeze network callback's no-movable-pieces branch only logs a TODO-style comment and does not advance the turn.
5. **Complete one RPC convention.** GameStat.cs:1304 applies both `[ClientRpc]` and `[Rpc(...)]` to LightningDestroyPawnClientRpc. The installed Netcode IL postprocessor iterates these attributes and retains the last valid one; this is not two intended calls and should be reduced to one attribute. Other methods mix legacy and universal RPC styles. Migrate deliberately while preserving permission and targeting semantics; older syntax alone is not evidence a method is unused.
6. **Avoid scene-rebuild polling as the normal state model.** The server heartbeat in GameStat.cs:866 rebuilds board/index every 0.5 seconds, and move requests rebuild again. Make piece registration/removal/movement consistent first. Keep rebuilds as recovery/diagnostics until trap, promotion, and resurrection lifecycles are proven; removing the heartbeat immediately could expose hidden dependencies.
7. **Standardize coordinate conversion.** Freeze, Divine Protection, and Teleport compute cells using a hard-coded 0.5 unit size. TapMoveController uses Tilemap.WorldToCell. Route targeting through the board/tilemap so origins/scales cannot silently diverge.
8. **Replace fragile identifiers.** MageAbilityController dispatches by display strings such as "Lightning Mage" and looks up MainCanvas by name. CardData already has an unused partial AbilityId model, while the dormant MageData model adds another. Choose one stable identifier/configuration model and inject the UI parent.
9. **Tidy lifecycle and initialization.** CardUI.OnEnable attempts the Resurrection graveyard subscription before a dynamically created card receives LoadCard(data); LoadCard does not establish that subscription afterward. ComputerPlayer and UGSBootstrap have async startup work without a shared cancellation/readiness contract. Return Task from internal async operations, retain async void only for required Unity/UI entry points, and gate Relay actions on initialization completion.
10. **Remove stale comments and dead members.** Examples: CardUI's comment says Resurrection lacks callbacks immediately after a branch binding Resurrection callbacks; DivineProtectionUI says BroadcastMessage although binding is direct; CharacterPanelUI retains the old disable block; VictoryUI.SetRematchInteractable is unused; GameStat retains verbose castling diagnostics and commented previous RPC calls.
11. **Names and layout.** GameStat.cs declares GameState, VictoryUI.cs declares VictoryPanelUI, KingSlayerSequenceControl.cs declares KingSlaySequenceController, and several other filenames differ from their types. Normalize deliberately with Unity metadata preserved. Move prefabs out of script directories and runtime mage classes out of ScriptableObjects asset directories when practical. Do not combine this asset move with a behavior rewrite.
12. **Repository hygiene.** Eleven `.vs` files are tracked and already produce local changes. Add an ignore rule and remove them from tracking in a separate reviewed change; an ignore rule alone does not untrack existing files. Keep recovery scenes and TextMesh Pro examples only if useful for development. Do not treat imported/vendor code as a target for stylistic rewrites.

## Preserve intentional game design

Storm Gambit intentionally supports king capture and survival of classical checkmate while magic remains. ChessMoveValidator is stricter than the movement-only ChessBoard.IsLegalMove helper and is used for classical escape/checkmate analysis. Their difference is not automatically obsolete duplication. Do not enforce classical king safety on every move as an incidental cleanup without confirming the intended Storm rules.

Also, a file with zero serialized references can still be essential: static evaluators, parsers, SpellCastState, and SpellCastLockToken have code consumers or are added at runtime. TeleportPulse is created with AddComponent. They are not removal candidates.

## Recommended implementation order

1. Repair hand counts and isolate draw schedules. Check initial hand, draw once, successful cast, cancelled cast, last-card use, and checkmate with/without mage ability in solo and host/client play.
2. Make network validation and accepted/rejected action responses consistent. Confirm invalid moves change no counters/resources and restore the client's visual position.
3. Consolidate victory and confirm both-player rematch, offline restart, menu return, and a second online session.
4. Remove confirmed unused prototype chains and detach legacy scene components in a separate cleanup change. Compile and inspect affected scenes/prefabs for missing references.
5. Extract shared state/move operations incrementally; then retire heartbeat recovery only when registration paths are reliable.
6. Perform naming, formatting, logging, and Git hygiene separately from behavioral fixes.

No percentage-complete estimate or release-readiness claim follows from this static review.
