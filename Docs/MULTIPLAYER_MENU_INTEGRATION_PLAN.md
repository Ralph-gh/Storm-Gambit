# Multiplayer menu integration plan

Status: implemented in LobbyScene. Unity compilation and all 18 lobby checks passed, including a Play-mode MainMenu → lobby → Host/Join → Back → MainMenu test and repeated screen reopening. Real two-client multiplayer and Android device validation remain pending; no Android device was connected during this session.

## Appearance and preservation

- Restore the original **Lobby of the Storm** as the multiplayer landing screen, keeping its existing artwork, button style, and text colors.
- Use the new long button artwork, **Lobby Long Button v3 - No Pieces.png**, only in the new multiplayer screens. Match their button label color to the existing menu text.
- Preserve the archived scene, old UI assets, prefabs, and scripts. Keep the archive as a reference rather than modifying it to serve as the live scene.
- Keep this integration focused on navigation and working multiplayer; defer additional visual polish and animation.

## Target navigation

1. Main Menu → Lobby of the Storm.
2. Lobby **Host** → new Host UI → Create Game → existing multiplayer hosting/waiting flow.
3. Lobby **Join** → new Join UI → available games → select a game and join; request a password for protected games.
4. Back from the new Host or Join UI → Lobby of the Storm.
5. Lobby **Back to Main Menu** → Main Menu.

The original landing screen's Host and Join buttons will open the new screens instead of invoking the original Relay-only connection controls. Its appearance remains unchanged.

## Integration constraints

- Reuse the existing Lobby/Relay backend and new browser/host components.
- Share the existing NetworkManager and service initialization. Do not activate a second manager by loading the archived scene alongside the current scene.
- Preserve connection cancellation, room cleanup, and transport shutdown when leaving a waiting room or pending connection.
- Return from a child screen to its parent without accidentally exiting multiplayer to Main Menu.

## Acceptance checks

- [x] Main Menu opens the original-looking Lobby of the Storm.
- [ ] Host opens the new Host UI and creates an open or password-protected game. Opening verified; real game creation needs a multiplayer test.
- [ ] Join opens the new browser, lists available games, and joins the selected game. Opening and row selection verified; real connection needs a multiplayer test.
- [x] Back from either child screen returns to Lobby of the Storm.
- [x] Back from Lobby of the Storm returns to Main Menu.
- [ ] Only one NetworkManager is active through navigation and connection attempts. Navigation verified; real connection attempts remain to test.
- [ ] Verify real host/join connections with two clients in Unity Editor and Android; verify cancellation and retry.

Implemented immediately at the user's subsequent request. No reminder was created or changed.
