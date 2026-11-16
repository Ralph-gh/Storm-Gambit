using System;
using System.Threading;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class RelayUI : MonoBehaviour
{
    [Header("UI (optional)")]
    [SerializeField] TMP_Text joinCodeLabel;
    [SerializeField] TMP_InputField joinCodeInput;
    [SerializeField] int maxClients = 1;

    public event Action<string> StatusChanged;
    private CancellationTokenSource pendingOperation;

    public void StartRelayHost() => StartRelay(true);
    public void StartRelayClient() => StartRelay(false);

    private async void StartRelay(bool host)
    {
        if (pendingOperation != null) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.GetComponent<UnityTransport>() == null)
        {
            StatusChanged?.Invoke("Network manager or transport is missing.");
            return;
        }
        if (nm.IsListening || nm.ShutdownInProgress) return;

        string code = joinCodeInput ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
        if (!host && string.IsNullOrEmpty(code))
        {
            StatusChanged?.Invoke("Enter the host's join code first.");
            return;
        }

        var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        pendingOperation = operation;
        bool startingNetwork = false;
        if (host && joinCodeLabel) joinCodeLabel.text = "";
        try
        {
            StatusChanged?.Invoke("Connecting to online services...");
            await RelayConnection.WaitAsync(UGSBootstrap.EnsureReadyAsync(), operation.Token);

            if (host)
            {
                StatusChanged?.Invoke("Creating host...");
                startingNetwork = true;
                string joinCode = await RelayConnection.HostAsync(maxClients, operation.Token);
                if (joinCodeLabel) joinCodeLabel.text = $"Code: {joinCode}";
                StatusChanged?.Invoke("Host ready. Waiting for opponent (1/2)...");
            }
            else
            {
                StatusChanged?.Invoke("Joining host...");
                startingNetwork = true;
                await RelayConnection.JoinAsync(code, operation.Token);
                StatusChanged?.Invoke("Connecting to host...");
            }
        }
        catch (Exception e)
        {
            if (startingNetwork && nm != null) nm.Shutdown();
            if (host && joinCodeLabel) joinCodeLabel.text = "";
            if (this != null && isActiveAndEnabled)
            {
                string message = e is OperationCanceledException
                    ? "Connection timed out. Check your internet and try again."
                    : "Connection failed: " + e.Message;
                Debug.LogWarning("[Relay] " + e);
                StatusChanged?.Invoke(message);
            }
        }
        finally
        {
            if (pendingOperation == operation) pendingOperation = null;
            operation.Dispose();
        }
    }

    private void OnDisable() => pendingOperation?.Cancel();
}
