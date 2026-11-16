using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
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
            await AwaitOperation(UGSBootstrap.EnsureReadyAsync(), operation.Token);
            var utp = nm.GetComponent<UnityTransport>();
            if (host)
            {
                StatusChanged?.Invoke("Creating host...");
                var allocation = await AwaitOperation(
                    RelayService.Instance.CreateAllocationAsync(maxClients), operation.Token);
                string joinCode = await AwaitOperation(
                    RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId), operation.Token);
                utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                startingNetwork = true;
                if (!nm.StartHost()) throw new InvalidOperationException("Host could not start.");
                if (joinCodeLabel) joinCodeLabel.text = $"Code: {joinCode}";
                StatusChanged?.Invoke("Host ready. Waiting for opponent (1/2)...");
            }
            else
            {
                StatusChanged?.Invoke("Joining host...");
                var allocation = await AwaitOperation(
                    RelayService.Instance.JoinAllocationAsync(code), operation.Token);
                utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                startingNetwork = true;
                if (!nm.StartClient()) throw new InvalidOperationException("Client could not start.");
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

    private static async Task AwaitOperation(Task task, CancellationToken token)
    {
        // Relay requests cannot be canceled; never start NGO after leaving the lobby.
        var canceled = Task.Delay(Timeout.Infinite, token);
        await Task.WhenAny(task, canceled);
        token.ThrowIfCancellationRequested();
        await task;
    }

    private static async Task<T> AwaitOperation<T>(Task<T> task, CancellationToken token)
    {
        await AwaitOperation((Task)task, token);
        return await task;
    }

    private void OnDisable() => pendingOperation?.Cancel();
}
