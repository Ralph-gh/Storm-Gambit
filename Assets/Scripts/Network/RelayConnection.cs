using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;

// Shared by the browser and the legacy join-code UI.
public static class RelayConnection
{
    private static UnityTransport Transport()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || manager.GetComponent<UnityTransport>() == null)
            throw new InvalidOperationException("Network manager or transport is missing.");
        if (manager.IsListening || manager.ShutdownInProgress)
            throw new InvalidOperationException("A network connection is already active or shutting down.");
        return manager.GetComponent<UnityTransport>();
    }

    public static async Task<string> HostAsync(int maxClients, CancellationToken token)
    {
        var transport = Transport();
        await WaitAsync(UGSBootstrap.EnsureReadyAsync(), token);
        var allocation = await WaitAsync(RelayService.Instance.CreateAllocationAsync(maxClients), token);
        string code = await WaitAsync(RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId), token);
        token.ThrowIfCancellationRequested();
        transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
        if (!NetworkManager.Singleton.StartHost())
            throw new InvalidOperationException("Host could not start.");
        return code;
    }

    public static async Task JoinAsync(string code, CancellationToken token)
    {
        var transport = Transport();
        await WaitAsync(UGSBootstrap.EnsureReadyAsync(), token);
        var allocation = await WaitAsync(RelayService.Instance.JoinAllocationAsync(code), token);
        token.ThrowIfCancellationRequested();
        transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
        if (!NetworkManager.Singleton.StartClient())
            throw new InvalidOperationException("Client could not start.");
    }

    public static async Task WaitAsync(Task task, CancellationToken token)
    {
        var canceled = new TaskCompletionSource<bool>();
        using (token.Register(() => canceled.TrySetResult(true)))
        {
            if (await Task.WhenAny(task, canceled.Task) != task)
            {
                // Observe a late SDK failure even though the user has left the lobby.
                _ = task.ContinueWith(t => { var ignored = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                token.ThrowIfCancellationRequested();
            }
            token.ThrowIfCancellationRequested();
            await task;
        }
    }

    public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken token)
    {
        await WaitAsync((Task)task, token);
        return await task;
    }
}
