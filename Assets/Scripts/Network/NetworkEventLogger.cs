using UnityEngine;
using Unity.Netcode;

public class NetworkEventsLogger : MonoBehaviour
{
    void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
    }

    void OnServerStarted()
    {
        var nm = NetworkManager.Singleton;
        var role = nm.IsHost ? "HOST" : nm.IsServer ? "SERVER" : "UNKNOWN";
        Debug.Log($"[NGO] Server started as {role}. LocalClientId={nm.LocalClientId}");
    }

    void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        string role = nm.IsHost ? "HOST" : nm.IsClient ? "CLIENT" : "UNKNOWN";
        Debug.Log($"[NGO] {role} sees client CONNECTED: {clientId}. MyLocalId={nm.LocalClientId}. Total={nm.ConnectedClientsIds.Count}");
    }

    void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        string role = nm.IsHost ? "HOST" : nm.IsClient ? "CLIENT" : "UNKNOWN";
        Debug.Log($"[NGO] {role} sees client DISCONNECTED: {clientId}. Total={nm.ConnectedClientsIds.Count}");
    }
}
