# Original lobby archive

**Scene:** `Assets/Scenes/Archive/LobbyScene_Legacy.unity`

This preserves the original Relay join-code lobby from commit `3e2b060` (the last commit before the game browser was added). The original Canvas is active in the archive, and its Host, Join, Back, join-code input, join-code display and status references are retained. The scene has its own Unity asset GUID.

The archive is a design reference and a fallback for the earlier join-code flow. It is not in EditorBuildSettings and is not loaded by normal MainMenu navigation. The active `LobbyScene` keeps its existing scene GUID and network setup. It now includes a copy of the original Canvas as its landing menu: Host and Join open the new screens, and Back returns to MainMenu. The archive itself has not been changed. The live scene does not include the old RelayUI component; optional legacy references on LobbySceneController remain cleared.

Open the archive directly in Unity to inspect it. To test the old multiplayer flow between clients, temporarily include the archive in the test build's scene list and start both clients in that scene. Normal builds should continue to include only MainMenu, LobbyScene and GameScene.

The archive reuses the project's current scripts, sprites, prefabs, authentication and Relay implementation. It is a preserved scene layout, not a frozen copy of the entire old codebase. Keep RelayUI.cs and the shared networking assets: the archive still uses them. The complete historical project remains available in Git at the source commit above.
