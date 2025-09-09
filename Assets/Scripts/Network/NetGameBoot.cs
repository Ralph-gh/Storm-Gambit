using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
//using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class NetGameBoot : MonoBehaviour
{
    [SerializeField] private GameObject gameStatePrefab; // assign GameState prefab in Inspector
    [SerializeField] private GameObject playerPrefab;    // assign NetPlayer prefab in Inspector

    string connectAddress = "192.168.2.10";
    ushort port = 7777;
    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || nm.IsClient || nm.IsServer) return;

        const int w = 220, h = 40;
        int x = 20, y = 20, pad = 10;


        GUI.Label(new Rect(x, y, w, h), "Host IP (for clients):");
        connectAddress = GUI.TextField(new Rect(x, y + h, w, h), connectAddress);
        GUI.Label(new Rect(x, y + 2 * h + pad, 100, h), "Port:");
        var portStr = GUI.TextField(new Rect(x + 50, y + 2 * h + pad, 70, h), port.ToString());
        ushort.TryParse(portStr, out port);

        // Start Host
        if (GUI.Button(new Rect(x, y + 3 * h + 2 * pad, w, h), "Start Host"))
        {
            var utp = nm.GetComponent<UnityTransport>();
            // Bind to all interfaces so clients can connect from LAN
            utp.SetConnectionData("0.0.0.0", port);
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.StartHost();

            // Spawn the GameState on the host
            var gs = Instantiate(gameStatePrefab);
            gs.GetComponent<NetworkObject>().Spawn(true);
        }

        // Start Client
        if (GUI.Button(new Rect(x, y + 4 * h + 3 * pad, w, h), "Start Client"))
        {
            var utp = nm.GetComponent<UnityTransport>();
            utp.SetConnectionData(connectAddress, port);
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.StartClient();
        }
    }
}
