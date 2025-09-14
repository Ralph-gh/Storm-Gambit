using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

// Optional TMP refs; remove if you don't use TextMeshPro
using TMPro;

public class RelayUI : MonoBehaviour
{
    [Header("UI (optional)")]
    [SerializeField] TMP_Text joinCodeLabel;       // shows code on host
    [SerializeField] TMP_InputField joinCodeInput;  // client enters code here
    [SerializeField] int maxClients = 1;            // chess: one opponent

    // INTERNET: Host via Relay (NEW BUTTON)
    public async void StartRelayHost()
    {
        try
        {
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxClients);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            Debug.Log("[Relay] Join Code: " + joinCode);
            if (joinCodeLabel) joinCodeLabel.text = $"Code: {joinCode}";

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(
                alloc.RelayServer.IpV4,
                (ushort)alloc.RelayServer.Port,
                alloc.AllocationIdBytes,
                alloc.Key,
                alloc.ConnectionData);

            bool ok = NetworkManager.Singleton.StartHost();
            Debug.Log(ok ? "[NGO] Host started via Relay" : "[NGO] Host start FAILED");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Relay] Host error: " + e);
        }
    }

    // INTERNET: Client via Relay (NEW BUTTON)
    public async void StartRelayClient()
    {
        string code = joinCodeInput ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[Relay] No join code entered.");
            return;
        }

        try
        {
            JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key,
                join.ConnectionData,
                join.HostConnectionData);

            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log(ok ? "[NGO] Client starting via Relay" : "[NGO] Client start FAILED");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Relay] Join error: " + e);
        }
    }
}
