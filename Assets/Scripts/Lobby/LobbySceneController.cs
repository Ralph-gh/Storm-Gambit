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

    private NetworkManager subscribedManager;
    private float connectionDeadline;
    private bool loadingGame;

    private void OnEnable()
    {
        if (relayUI != null) relayUI.StatusChanged += UpdateStatus;
        SubscribeToNetwork();
    }

    private void OnDisable()
    {
        if (relayUI != null) relayUI.StatusChanged -= UpdateStatus;
        if (subscribedManager != null)
        {
            subscribedManager.OnClientConnectedCallback -= HandleClientConnected;
            subscribedManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            subscribedManager.OnTransportFailure -= HandleTransportFailure;
        }
        subscribedManager = null;
    }

    private void Start()
    {
        // All scene Awakes have run now; Singleton can be null during OnEnable.
        SubscribeToNetwork();
        UpdateStatus("Not Connected");
    }

    private void SubscribeToNetwork()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || subscribedManager == nm) return;
        if (subscribedManager != null)
        {
            subscribedManager.OnClientConnectedCallback -= HandleClientConnected;
            subscribedManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            subscribedManager.OnTransportFailure -= HandleTransportFailure;
        }
        subscribedManager = nm;
        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;
        nm.OnTransportFailure += HandleTransportFailure;
    }

    private void Update()
    {
        var nm = subscribedManager;
        if (nm == null || connectionDeadline == 0) return;
        if (nm.IsConnectedClient) { connectionDeadline = 0; return; }
        if (Time.realtimeSinceStartup < connectionDeadline) return;
        connectionDeadline = 0;
        nm.Shutdown();
        UpdateStatus("Connection timed out. Check the host and join code, then try again.");
    }

    private void HandleTransportFailure()
    {
        connectionDeadline = 0;
        UpdateStatus("Network connection failed. Check your internet and try again.");
    }

    public void HostGame()
    {
        SubscribeToNetwork();
        if (!CanStartConnection()) return;
        loadingGame = false;
        UpdateStatus("Creating host...");
        relayUI.StartRelayHost();
    }

    public void JoinGame()
    {
        SubscribeToNetwork();
        if (!CanStartConnection()) return;
        UpdateStatus("Joining host...");
        relayUI.StartRelayClient();
    }

    private bool CanStartConnection()
    {
        if (relayUI == null || subscribedManager == null)
        {
            UpdateStatus("Lobby network setup is missing.");
            return false;
        }
        return !subscribedManager.IsListening && !subscribedManager.ShutdownInProgress;
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

        connectionDeadline = 0;
        int count = nm.IsServer ? nm.ConnectedClientsIds.Count : 2;
        UpdateStatus($"Players Connected: {count}/2");

        // When host sees 2 players, load the game for everyone
        if (nm.IsHost && count >= 2 && !loadingGame)
        {
            loadingGame = true;
            UpdateStatus("Both players connected. Loading game...");
            var result = nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                loadingGame = false;
                UpdateStatus("Could not load game: " + result);
            }
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.IsServer)
        {
            connectionDeadline = 0;
            UpdateStatus("Disconnected from host. " + nm.DisconnectReason);
            return;
        }

        int count = nm.ConnectedClientsIds.Count;
        UpdateStatus($"Player disconnected. Players Connected: {count}/2");
    }

    private void UpdateStatus(string message)
    {
        if (message == "Connecting to host...")
            connectionDeadline = Time.realtimeSinceStartup + 30f;
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[Lobby] " + message);
    }
}
