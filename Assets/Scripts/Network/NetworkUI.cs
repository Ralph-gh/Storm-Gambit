using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkUI : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] string connectAddress = "127.0.0.1";
    [SerializeField] ushort port = 7777;
    [SerializeField] GameObject gameStatePrefab; // drag your GameState prefab if you have one
    [SerializeField] GameObject playerPrefab;    // drag your NetPlayer prefab

    void Awake()
    {
        var nm = NetworkManager.Singleton;
        if (!nm) { Debug.LogError("[NGO] No NetworkManager in scene."); return; }

        // Ensure Transport has address/port
        var utp = nm.GetComponent<UnityTransport>();
        if (utp != null) utp.SetConnectionData(connectAddress, port);

        // Ensure Player Prefab is assigned (so one spawns per connection)
        if (playerPrefab) nm.NetworkConfig.PlayerPrefab = playerPrefab;
    }

    // Hook to your "Host" button
    public void StartHost()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || nm.IsListening) return;

        // Host should bind to all interfaces so clone can connect via localhost/LAN
        var utp = nm.GetComponent<UnityTransport>();
        if (utp != null) utp.SetConnectionData("0.0.0.0", port);

        bool ok = nm.StartHost();
        Debug.Log(ok ? "[NGO] Host started." : "[NGO] Host FAILED to start.");

        // Spawn GameState on the server if you use it
        if (ok && gameStatePrefab)
        {
            var go = Instantiate(gameStatePrefab);
            var no = go.GetComponent<NetworkObject>();
            if (no) no.Spawn(true);
        }
    }

    // Hook to your "Client" button
    public void StartClient()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || nm.IsListening) return;

        var utp = nm.GetComponent<UnityTransport>();
        if (utp != null) utp.SetConnectionData(connectAddress, port);

        bool ok = nm.StartClient();
        Debug.Log(ok ? "[NGO] Client starting..." : "[NGO] Client FAILED to start.");
    }

    // Hook to your "Shutdown" button (optional)
    public void Shutdown()
    {
        var nm = NetworkManager.Singleton;
        if (nm && nm.IsListening) nm.Shutdown();
        Debug.Log("[NGO] Shutdown.");
    }

    // Optional: call from a UI InputField if you want to change IP at runtime
    public void SetAddress(string addr)
    {
        connectAddress = string.IsNullOrEmpty(addr) ? "127.0.0.1" : addr;
    }
}
