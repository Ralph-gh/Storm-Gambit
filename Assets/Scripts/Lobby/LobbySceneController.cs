using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;

public class LobbySceneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RelayUI relayUI;
    [SerializeField] private TMP_Text statusText;

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void Start()
    {
        UpdateStatus("Not Connected");
    }

    public void HostGame()
    {
        UpdateStatus("Creating host...");
        relayUI.StartRelayHost();
    }

    public void JoinGame()
    {
        UpdateStatus("Joining host...");
        relayUI.StartRelayClient();
    }

    public void BackToMainMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void HandleClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        int count = nm.ConnectedClientsIds.Count;
        UpdateStatus($"Players Connected: {count}/2");

        // When host sees 2 players, load the game for everyone
        if (nm.IsHost && count >= 2)
        {
            UpdateStatus("Both players connected. Loading game...");
            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        int count = nm.ConnectedClientsIds.Count;
        UpdateStatus($"Player disconnected. Players Connected: {count}/2");
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[Lobby] " + message);
    }
}