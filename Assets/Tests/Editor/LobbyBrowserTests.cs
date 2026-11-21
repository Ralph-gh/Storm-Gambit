using System;
using NUnit.Framework;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Collections;

public class LobbyBrowserTests
{
    [Test]
    public void LegacyLobbyIsPreservedSeparatelyAndExcludedFromBuild()
    {
        const string archivePath = "Assets/Scenes/Archive/LobbyScene_Legacy.unity";
        foreach (var entry in EditorBuildSettings.scenes) Assert.AreNotEqual(archivePath, entry.path);
        Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(archivePath),
            AssetDatabase.AssetPathToGUID("Assets/Scenes/LobbyScene.unity"));
        var legacy = EditorSceneManager.OpenPreviewScene(archivePath);
        var current = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            RelayUI relay = null;
            LobbySceneController controller = null;
            bool canvasActive = false;
            foreach (var root in legacy.GetRootGameObjects())
            {
                Assert.IsNull(root.GetComponentInChildren<LobbyBrowserUI>(true));
                var foundRelay = root.GetComponentInChildren<RelayUI>(true);
                if (foundRelay != null) relay = foundRelay;
                var foundController = root.GetComponentInChildren<LobbySceneController>(true);
                if (foundController != null) controller = foundController;
                if (root.name == "Canvas") canvasActive = root.activeSelf;
            }
            Assert.IsTrue(canvasActive, "The original lobby must remain usable in its archive.");
            Assert.IsNotNull(relay);
            Assert.IsNotNull(controller);
            Assert.IsNotNull(new SerializedObject(relay).FindProperty("joinCodeLabel").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(relay).FindProperty("joinCodeInput").objectReferenceValue);
            Assert.AreSame(relay, new SerializedObject(controller).FindProperty("relayUI").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(controller).FindProperty("statusText").objectReferenceValue);
            foreach (var root in current.GetRootGameObjects())
            {
                Assert.IsNull(root.GetComponentInChildren<RelayUI>(true), "Legacy UI belongs only to the archive.");
                Assert.AreNotEqual("Canvas", root.name);
            }
            LobbySceneSetup.Validate(Browser(current));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(current);
            EditorSceneManager.ClosePreviewScene(legacy);
        }
    }

    private static LobbyBrowserUI Browser(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.TryGetComponent<LobbyBrowserUI>(out var browser)) return browser;
        throw new InvalidOperationException("Lobby browser was not saved in the scene.");
    }

    [UnityTest]
    public IEnumerator MainMenuLandingHostJoinAndBackWorkInPlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        var main = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
        Assert.IsNotNull(main);
        main.OpenMultiplayerLobby();
        float deadline = Time.realtimeSinceStartup + 15;
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene" && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;
        var navigation = UnityEngine.Object.FindFirstObjectByType<LobbyMenuNavigation>();
        Assert.IsNotNull(navigation);
        var browser = navigation.browser;
        Assert.IsTrue(navigation.landing.activeInHierarchy);
        for (int i = 0; i < 3; i++)
        {
            navigation.hostButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(browser.hostPanel.activeInHierarchy);
            Assert.IsFalse(navigation.landing.activeInHierarchy);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length);
            browser.backButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 10;
            while (!navigation.landing.activeInHierarchy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(navigation.landing.activeInHierarchy);
            navigation.joinButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(browser.browsePanel.activeInHierarchy);
            Assert.IsFalse(browser.hostPanel.activeInHierarchy);
            browser.backButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 10;
            while (!navigation.landing.activeInHierarchy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(navigation.landing.activeInHierarchy);
        }
        navigation.mainMenuButton.onClick.Invoke();
        deadline = Time.realtimeSinceStartup + 10;
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual("MainMenu", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        yield return new ExitPlayMode();
    }

    [Test]
    public void OriginalLandingRoutesToNewScreensAndReturnsWithoutExtraNetworkObjects()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var browser = Browser(scene);
            var navigation = browser.navigation;
            Assert.IsNotNull(navigation);
            Assert.AreSame(browser, navigation.browser);
            Assert.AreSame(browser.controller.gameObject, navigation.gameObject);
            Assert.IsTrue(navigation.landing.activeSelf);
            Assert.IsFalse(browser.gameObject.activeSelf);
            Assert.AreEqual(0, navigation.landing.GetComponentsInChildren<TMPro.TMP_InputField>().Length,
                "The landing screen must not ask for an unused Relay code.");
            Assert.AreEqual(0, navigation.hostButton.onClick.GetPersistentEventCount());
            Assert.AreEqual(0, navigation.joinButton.onClick.GetPersistentEventCount());
            Assert.AreEqual(0, navigation.landing.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true).Length);
            int managers = 0;
            foreach (var root in scene.GetRootGameObjects()) managers += root.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true).Length;
            Assert.AreEqual(1, managers);
            for (int i = 0; i < 3; i++)
            {
                navigation.OpenHost();
                Assert.IsFalse(navigation.landing.activeSelf);
                Assert.IsTrue(browser.gameObject.activeSelf);
                Assert.IsTrue(browser.hostPanel.activeSelf);
                Assert.IsFalse(browser.browsePanel.activeSelf);
                browser.ReturnToLobby();
                Assert.IsTrue(navigation.landing.activeSelf);
                Assert.IsFalse(browser.gameObject.activeSelf);
                navigation.OpenJoin();
                Assert.IsTrue(browser.browsePanel.activeSelf);
                Assert.IsFalse(browser.hostPanel.activeSelf);
                browser.ReturnToLobby();
                Assert.IsTrue(navigation.landing.activeSelf);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void NameEditorKeepsTheSavedNameWhenValidationFails()
    {
        const string key = "StormGambit.PlayerName";
        bool existed = PlayerPrefs.HasKey(key);
        string original = PlayerPrefs.GetString(key);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var browser = Browser(scene);
            browser.playerNameInput.text = "Storm Mage";
            browser.SavePlayerName();
            Assert.AreEqual("Storm Mage", LobbyIdentity.PlayerName);
            Assert.IsTrue(browser.identitySummary.activeSelf);
            Assert.IsFalse(browser.identityEditor.activeSelf);
            Assert.IsFalse(browser.feedbackPanel.activeSelf);
            browser.BeginNameEdit();
            Assert.IsTrue(browser.identityEditor.activeSelf);
            browser.playerNameInput.text = "x";
            browser.SavePlayerName();
            Assert.AreEqual("Storm Mage", LobbyIdentity.PlayerName);
            Assert.IsTrue(browser.feedbackPanel.activeSelf);
            Assert.IsTrue(browser.identityEditor.activeSelf);
        }
        finally
        {
            if (existed) PlayerPrefs.SetString(key, original); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void BrowserDistinguishesEmptyErrorAndPopulatedStates()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var browser = Browser(scene);
            var games = new System.Collections.Generic.List<Lobby>();
            browser.ShowFeedback(null);
            browser.PresentGames(games, false, true, null);
            Assert.AreEqual("No games available", browser.emptyTitle.text);
            Assert.IsTrue(browser.emptyHostButton.gameObject.activeSelf);
            Assert.IsFalse(browser.retryButton.gameObject.activeSelf);
            Assert.IsFalse(browser.feedbackPanel.activeSelf);
            browser.PresentGames(games, false, true, "Connection failed.");
            Assert.IsTrue(browser.retryButton.gameObject.activeSelf);
            Assert.IsFalse(browser.emptyHostButton.gameObject.activeSelf);
            StringAssert.Contains("Connection failed", browser.emptyText.text);
            browser.PresentGames(new System.Collections.Generic.List<Lobby>
            {
                new Lobby(id: "room", name: "A game", maxPlayers: 2, availableSlots: 1)
            }, false, true, null);
            Assert.IsFalse(browser.emptyState.activeSelf);
            Assert.IsTrue(browser.rows.gameObject.activeSelf);
            Assert.AreEqual("1 game available", browser.gameCountText.text);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void TabSelectionHasAPersistentIndicator()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var browser = Browser(scene);
            LobbyUIBuilder.SetTabSelected(browser.browseTab, false);
            LobbyUIBuilder.SetTabSelected(browser.hostTab, true);
            Assert.IsFalse(browser.browseTab.transform.Find("Selected").gameObject.activeSelf);
            Assert.IsTrue(browser.hostTab.transform.Find("Selected").gameObject.activeSelf);
            Assert.AreNotEqual(browser.browseTab.GetComponent<Image>().color, browser.hostTab.GetComponent<Image>().color);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("  Storm Mage  ", "Storm Mage")]
    [TestCase("Ralph", "Ralph")]
    public void NamesAreTrimmed(string value, string expected) =>
        Assert.AreEqual(expected, LobbyIdentity.ValidateName(value));

    [TestCase("")]
    [TestCase("x")]
    [TestCase("Name\nsecond line")]
    [TestCase("<b>Name</b>")]
    [TestCase("Name longer than twenty four characters")]
    public void InvalidNamesAreRejected(string value) =>
        Assert.Throws<ArgumentException>(() => LobbyIdentity.ValidateName(value));

    [Test]
    public void PasswordUsesServiceValidationAndRelayIsNotPublic()
    {
        var options = LobbyMatchmaking.CreateOptions("Ralph", "RELAYCODE", "swordfish");
        Assert.AreEqual("swordfish", options.Password);
        Assert.AreEqual(false, options.IsPrivate);
        Assert.AreEqual(DataObject.VisibilityOptions.Member, options.Data[LobbyMatchmaking.RelayKey].Visibility);
        Assert.AreEqual(DataObject.VisibilityOptions.Public, options.Data[LobbyMatchmaking.NameKey].Visibility);
        Assert.AreEqual(DataObject.IndexOptions.S1, options.Data[LobbyMatchmaking.RulesKey].Index);
        Assert.IsFalse(options.Data.ContainsKey("password"));
        Assert.AreEqual("Ralph", options.Player.Data[LobbyMatchmaking.NameKey].Value);
    }

    [Test]
    public void OpenGamesDoNotCarryAPassword()
    {
        Assert.IsNull(LobbyIdentity.ValidatePassword("leftover-secret", false));
        Assert.IsNull(LobbyMatchmaking.CreateOptions("Ralph", "CODE", null).Password);
        Assert.Throws<ArgumentException>(() => LobbyIdentity.ValidatePassword("short", true));
        Assert.Throws<ArgumentException>(() => LobbyIdentity.ValidatePassword(new string('x', 65), true));
        Assert.AreEqual("abcdefgh", LobbyIdentity.ValidatePassword("abcdefgh", true));
    }

    [Test]
    public void CommonJoinFailuresHaveSpecificFeedback()
    {
        StringAssert.StartsWith("Incorrect password", LobbyMatchmaking.FriendlyError(
            new LobbyServiceException(LobbyExceptionReason.IncorrectPassword, "secret service details")));
        StringAssert.Contains("full", LobbyMatchmaking.FriendlyError(
            new LobbyServiceException(LobbyExceptionReason.LobbyFull, "details")));
        StringAssert.Contains("no longer available", LobbyMatchmaking.FriendlyError(
            new LobbyServiceException(LobbyExceptionReason.LobbyNotFound, "details")));
        StringAssert.DoesNotContain("secret service details", LobbyMatchmaking.FriendlyError(
            new LobbyServiceException(LobbyExceptionReason.IncorrectPassword, "secret service details")));
    }

    [Test]
    public void LobbySceneContainsWiredMobileUi()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            LobbyBrowserUI browser = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponent<LobbyBrowserUI>();
                if (candidate != null) browser = candidate;
            }
            Assert.IsNotNull(browser, "The browser must be baked into LobbyScene.");
            LobbySceneSetup.Validate(browser);
            Assert.IsNotNull(browser.GetComponentInChildren<LobbySafeArea>());
            Assert.IsNotNull(browser.GetComponent<GraphicRaycaster>());
            Assert.AreEqual(TMPro.TMP_InputField.ContentType.Password, browser.hostPasswordInput.contentType);
            Assert.AreEqual(TMPro.TMP_InputField.ContentType.Password, browser.joinPasswordInput.contentType);
            Assert.IsFalse(browser.passwordDialog.activeSelf);
            Assert.IsFalse(browser.rowTemplate.gameObject.activeSelf);
            Assert.IsFalse(browser.playerNameInput.richText);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void ProtectedRowShowsHostAndJoinsTheSelectedGame()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            LobbyBrowserUI browser = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<LobbyBrowserUI>(out var found)) browser = found;
            var room = new Lobby(id: "protected-room", name: "Storm arena", maxPlayers: 2, availableSlots: 1,
                hasPassword: true, data: new System.Collections.Generic.Dictionary<string, DataObject>
                { { LobbyMatchmaking.NameKey, new DataObject(DataObject.VisibilityOptions.Public, "Ralph") } });
            var row = UnityEngine.Object.Instantiate(browser.rowTemplate, browser.rows);
            Lobby selected = null;
            row.Bind(room, chosen => selected = chosen);
            Assert.AreEqual("Storm arena", row.roomName.text);
            StringAssert.Contains("Ralph", row.hostName.text);
            StringAssert.Contains("PASSWORD", row.access.text);
            row.joinButton.onClick.Invoke();
            Assert.AreSame(room, selected);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
