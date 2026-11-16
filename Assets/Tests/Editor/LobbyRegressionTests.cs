using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class LobbyRegressionTests
{
    private GameObject lobbyObject;
    private GameObject networkObject;
    private LobbySceneController lobby;
    private NetworkManager manager;
    private TMP_Text status;
    private RelayUI relay;

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        // Reproduce the scene order: lobby OnEnable runs before the singleton exists.
        lobbyObject = new GameObject("Lobby");
        relay = lobbyObject.AddComponent<RelayUI>();
        lobby = lobbyObject.AddComponent<LobbySceneController>();
        var textObject = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(lobbyObject.transform);
        status = textObject.GetComponent<TMP_Text>();
        Set(lobby, "statusText", status);
        Set(lobby, "relayUI", relay);
        // Serialized references normally exist before OnEnable.
        lobby.enabled = false;
        lobby.enabled = true;
        networkObject = new GameObject("Network");
        manager = networkObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = networkObject.AddComponent<UnityTransport>();
        manager.NetworkConfig.EnableSceneManagement = false;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        manager.Shutdown();
        yield return null;
        Object.DestroyImmediate(lobbyObject);
        Object.DestroyImmediate(networkObject);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LateNetworkManagerStillReportsHostConnection()
    {
        Assert.IsTrue(manager.StartHost());
        yield return null;
        Assert.AreEqual("Players Connected: 1/2", status.text);
    }

    [Test]
    public void EmptyJoinCodeDisplaysActionableStatus()
    {
        lobby.JoinGame();
        Assert.AreEqual("Enter the host's join code first.", status.text);
        Assert.IsFalse(manager.IsListening);
    }

    [UnityTest]
    public IEnumerator ReenabledLobbyStillReceivesConnectionCallbacks()
    {
        lobby.enabled = false;
        lobby.enabled = true;
        Assert.IsTrue(manager.StartHost());
        yield return null;
        Assert.AreEqual("Players Connected: 1/2", status.text);
    }

    [Test]
    public void ConnectionDeadlineStopsWaiting()
    {
        Set(lobby, "connectionDeadline", -1f);
        Call(lobby, "Update");
        StringAssert.StartsWith("Connection timed out.", status.text);
    }
}
