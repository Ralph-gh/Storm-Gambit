# Multiplayer game browser

`Assets/Scenes/LobbyScene.unity` opens with **Lobby of the Storm - Original Menu**, a copy of the archived lobby's Canvas with its appearance preserved. Host and Join now open the existing **Multiplayer Lobby UI** hosting and browser screens. Back from either new screen cancels pending multiplayer work, leaves any waiting room, and returns to the original menu; its Back button returns to MainMenu. The archived Relay-only scene remains unchanged at `Assets/Scenes/Archive/LobbyScene_Legacy.unity` and excluded from the build. Only its Canvas was copied: the live scene keeps its existing controller, services and single NetworkManager.

The new screens use `LobbyLongButtonFrame` for long actions, sourced from **Lobby Long Button v3 - No Pieces.png**, with the original menu's gold label color. The original menu's buttons and artwork are unchanged. `LobbyMenuNavigation` switches screens without loading the archived scene. Browser subscriptions detach while hidden and reattach on reopening.

The live landing copy retains the old join-code panel/input and connection status objects as inactive references. The archived scene keeps those controls active for its original flow.

## Player flow

1. Enter a display name (2–24 characters) and select **Save**. It is remembered on this installation and appears in a compact **Playing as** row with an **Edit** action; authentication stays anonymous. Invalid edits preserve the previously saved name.
2. **Find a game** shows compatible, open two-player rooms, with game name, host name, access type and player count. The list refreshes every six seconds. Manual **Refresh** and error-state **Try again** actions are throttled to one request every two seconds.
3. **Host a game** accepts an optional game name and optional password. Blank game names default to the player's name. Protected rooms remain visible in the browser, but joining requires the password. Unity Lobby requires 8–64 characters for a password.
4. A host waits in their room until an opponent connects. The existing network scene loader then starts GameScene for both players. The public listing is removed when the match connects.
5. Cancel/leave or Back removes the membership/listing and stops the transport. Hosts send a heartbeat every 15 seconds while waiting; interrupted/abandoned hosts also expire from discovery through Unity Lobby.

## Services and data

- Added `com.unity.services.lobby` 1.3.0 alongside the existing Relay SDK; the project's resolved dependencies are in `Packages/packages-lock.json`.
- Uses the existing Unity project, environment and anonymous sign-in. Lobby must be available for this project in Unity Gaming Services. If queries are denied, check that service's configuration in the Unity Dashboard.
- Password validation is performed by Unity Lobby. Passwords are not stored in public custom lobby data, PlayerPrefs, or logs.
- Host name and the compatibility marker are public listing data. The Relay join code and player membership names are member-only data.
- Display names are also replicated on `NetPlayer.PlayerName` for future in-game UI/profile integration. Profiles, accounts and editable persistent profile data are not part of this change.
- Compatibility marker: `LobbyMatchmaking.RulesVersion`. Increment this when introducing incompatible network protocol changes.
- Both players must use this updated build; older Relay-only builds cannot discover these rooms.

## Editing and verification

The Canvas and its panels/fields/buttons can be edited directly in the scene. `Tools > Storm Gambit > Rebuild Multiplayer Lobby UI` regenerates the default layout and replaces edits to that Canvas; save the scene first. The template for game rows is inactive under the Browse games panel.

`Tools > Storm Gambit > Integrate Original Lobby Menus` installs or repairs the navigation and new long buttons without rebuilding the existing browser layout. It refuses to edit a dirty LobbyScene or run in Play mode.

`Tools > Storm Gambit > Validate Multiplayer Lobby` runs the browser regression checks. `Preview Multiplayer Lobby` renders local 1080×1920 screenshots into `.utmp/`. The checks cover input validation, password options, Relay-data visibility, scene references, masked password fields and selecting the correct room from a row.

Before release, test with two updated clients:

- Host an open game, discover it on the other device, and join.
- Repeat with a password; verify a wrong password is rejected and the correct one connects.
- Cancel a waiting room and verify it disappears on refresh.
- Leave during connection, retry, and verify no stale membership or transport remains.
- Check long names, list scrolling, phone safe areas and the Android keyboard.
- Confirm GameScene loads on both clients and names are present on their NetPlayer components.

Local screenshots, compiler outputs and test reports under `.utmp/` are generated validation artifacts.

## Lobby readability pass

- The existing storm artwork is cropped to the battlefield and used as a subdued background. Header text is rendered independently so the original picture's lettering does not compete with the controls.
- The selected tab has a persistent gold fill and underline. Secondary actions use simple gold-edged buttons, while Host, Join, Create and Main Menu retain the existing ornate button artwork.
- Control heights target 120 reference pixels on the 1080-wide Canvas; typography and touch targets should still be checked on physical phones.
- Empty, loading and query-error states have distinct messages. Empty rooms offer a Host action; errors offer Retry. Idle connection feedback is hidden, removing the duplicate empty-state message.
- Host settings scroll on shorter screens. Password prompts and connection progress use the same type sizes and control treatment.
- Local previews include empty and populated browsers, a taller screen, first-time name entry, error feedback, hosting, waiting and password entry. Populated preview rooms are sample data used only by the editor renderer; they are never published to Unity Lobby.
