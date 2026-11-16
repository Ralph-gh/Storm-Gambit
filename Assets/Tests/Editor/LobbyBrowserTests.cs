using System;
using NUnit.Framework;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class LobbyBrowserTests
{
    private static LobbyBrowserUI Browser(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.TryGetComponent<LobbyBrowserUI>(out var browser)) return browser;
        throw new InvalidOperationException("Lobby browser was not saved in the scene.");
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
