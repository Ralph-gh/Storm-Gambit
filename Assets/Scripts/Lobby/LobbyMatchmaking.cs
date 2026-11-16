using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using LobbyModel = Unity.Services.Lobbies.Models.Lobby;

// Lives through the game scene so waiting rooms keep their heartbeat and always get cleaned up.
public class LobbyMatchmaking : MonoBehaviour
{
    public const string RulesKey = "storm-rules";
    public const string RulesVersion = "storm-gambit-v2";
    public const string RelayKey = "relay-code";
    public const string NameKey = "display-name";
    public static LobbyMatchmaking Instance { get; private set; }
    public LobbyModel Room { get; private set; }
    public bool Busy { get; private set; }
    public bool Refreshing { get; private set; }
    public bool HasQueried { get; private set; }
    public string LastQueryError { get; private set; }
    public bool CanManualRefresh => !Refreshing && !Busy && !leaving && Room == null &&
        Time.realtimeSinceStartup >= nextManualRefresh;
    public string Status { get; private set; } = "Choose a game or host your own.";
    public IReadOnlyList<LobbyModel> Games { get; private set; } = new List<LobbyModel>();
    public event Action Changed;
    public bool IsHost => Room != null && Room.HostId == AuthenticationService.Instance.PlayerId;

    private NetworkManager manager;
    private CancellationTokenSource operation;
    private Task activeOperation = Task.CompletedTask;
    private float nextRefresh;
    private float nextManualRefresh;
    private float nextHeartbeat;
    private float nextPoll;
    private bool maintaining;
    private bool matchStarted;
    private bool leaving;
    private bool closingListing;
    private int browserUsers;
    private Task leaveTask;

    public static LobbyMatchmaking GetOrCreate()
    {
        if (Instance == null) new GameObject("Lobby Matchmaking Session").AddComponent<LobbyMatchmaking>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void AttachBrowser() { browserUsers++; SubscribeNetwork(); }
    public void DetachBrowser() { browserUsers = Mathf.Max(0, browserUsers - 1); }

    private void SubscribeNetwork()
    {
        var current = NetworkManager.Singleton;
        if (manager == current) return;
        UnsubscribeNetwork();
        manager = current;
        if (manager == null) return;
        manager.OnClientConnectedCallback += OnConnected;
        manager.OnClientDisconnectCallback += OnDisconnected;
        manager.OnTransportFailure += OnTransportFailure;
    }

    private void UnsubscribeNetwork()
    {
        if (manager == null) return;
        manager.OnClientConnectedCallback -= OnConnected;
        manager.OnClientDisconnectCallback -= OnDisconnected;
        manager.OnTransportFailure -= OnTransportFailure;
        manager = null;
    }

    private void Update()
    {
        SubscribeNetwork();
        if (!Busy && !leaving && !matchStarted && browserUsers > 0 && Room == null &&
            !Refreshing && Time.realtimeSinceStartup >= nextRefresh)
            _ = RefreshAsync();
        if (Room != null && !Busy && !leaving && !matchStarted && !maintaining)
        {
            if (IsHost && Time.realtimeSinceStartup >= nextHeartbeat) _ = MaintainAsync(true);
            else if (Time.realtimeSinceStartup >= nextPoll) _ = MaintainAsync(false);
        }
    }

    public async Task RefreshAsync(bool manual = false)
    {
        if (Refreshing || Busy || leaving || Room != null ||
            Time.realtimeSinceStartup < (manual ? nextManualRefresh : nextRefresh)) return;
        Refreshing = true;
        LastQueryError = null;
        nextManualRefresh = Time.realtimeSinceStartup + 2f;
        nextRefresh = Time.realtimeSinceStartup + 6f;
        Changed?.Invoke();
        try
        {
            await UGSBootstrap.EnsureReadyAsync();
            var result = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    new QueryFilter(QueryFilter.FieldOptions.S1, RulesVersion, QueryFilter.OpOptions.EQ)
                },
                Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.Created) }
            });
            Games = result.Results.FindAll(room => !room.IsLocked && room.AvailableSlots > 0);
            HasQueried = true;
            if (Room == null && !Busy && !leaving)
                SetStatus(Games.Count == 0 ? "No open games yet. Be the first to host." : "Choose a game or host your own.");
        }
        catch (Exception e)
        {
            LastQueryError = FriendlyError(e);
            if (!Busy && Room == null) SetStatus(LastQueryError);
        }
        finally { Refreshing = false; Changed?.Invoke(); }
    }

    public Task HostAsync(string playerName, string roomName, string password, bool protect)
    {
        if (Busy || leaving || Room != null) return Task.CompletedTask;
        return activeOperation = RunAsync(async token =>
        {
            playerName = LobbyIdentity.ValidateName(playerName);
            roomName = LobbyIdentity.ValidateRoomName(roomName, playerName);
            password = LobbyIdentity.ValidatePassword(password, protect);
            LobbyIdentity.SaveName(playerName);
            SetStatus("Creating your game...");
            await RelayConnection.WaitAsync(UGSBootstrap.EnsureReadyAsync(), token);
            token.ThrowIfCancellationRequested();
            SubscribeNetwork();
            string relayCode = await RelayConnection.HostAsync(1, token);
            // Publish only after the host transport is ready. Relay data is members-only.
            Room = await LobbyService.Instance.CreateLobbyAsync(roomName, 2,
                CreateOptions(playerName, relayCode, password));
            token.ThrowIfCancellationRequested();
            nextHeartbeat = Time.realtimeSinceStartup + 15f;
            nextPoll = Time.realtimeSinceStartup + 4f;
            SetStatus("Your game is open. Waiting for an opponent...");
        });
    }

    public Task JoinAsync(LobbyModel game, string playerName, string password)
    {
        if (Busy || leaving || Room != null) return Task.CompletedTask;
        return activeOperation = RunAsync(async token =>
        {
            if (game == null) throw new ArgumentException("Choose a game first.");
            playerName = LobbyIdentity.ValidateName(playerName);
            if (game.HasPassword) LobbyIdentity.ValidatePassword(password, true);
            LobbyIdentity.SaveName(playerName);
            SetStatus("Joining game...");
            await RelayConnection.WaitAsync(UGSBootstrap.EnsureReadyAsync(), token);
            Room = await LobbyService.Instance.JoinLobbyByIdAsync(game.Id, new JoinLobbyByIdOptions
            {
                Password = game.HasPassword ? password : null,
                Player = CreatePlayer(playerName)
            });
            token.ThrowIfCancellationRequested();
            if (Room.Data == null || !Room.Data.TryGetValue(RelayKey, out var relay))
                throw new InvalidOperationException("This game is not ready. Refresh and try another game.");
            SubscribeNetwork();
            SetStatus("Connecting to your opponent...");
            await RelayConnection.JoinAsync(relay.Value, token);
            while (manager != null && !manager.IsConnectedClient)
            {
                token.ThrowIfCancellationRequested();
                if (!manager.IsListening || manager.ShutdownInProgress)
                    throw new InvalidOperationException("The host disconnected. Refresh and try again.");
                await Task.Delay(100, token);
            }
            SetStatus("Opponent connected. Loading game...");
        });
    }

    public static CreateLobbyOptions CreateOptions(string playerName, string relayCode, string password)
    {
        return new CreateLobbyOptions
        {
            IsPrivate = false,
            Password = password,
            Player = CreatePlayer(playerName),
            Data = new Dictionary<string, DataObject>
            {
                { RulesKey, new DataObject(DataObject.VisibilityOptions.Public, RulesVersion, DataObject.IndexOptions.S1) },
                { NameKey, new DataObject(DataObject.VisibilityOptions.Public, playerName) },
                { RelayKey, new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
            }
        };
    }

    private static Player CreatePlayer(string name) => new Player(
        data: new Dictionary<string, PlayerDataObject>
        {
            { NameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, name) }
        });

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        Busy = true;
        matchStarted = false;
        operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        Changed?.Invoke();
        try { await action(operation.Token); }
        catch (Exception e)
        {
            await ClearRoomAsync();
            if (manager != null && manager.IsListening) manager.Shutdown();
            if (!leaving) SetStatus(FriendlyError(e));
        }
        finally
        {
            operation.Dispose();
            operation = null;
            Busy = false;
            Changed?.Invoke();
        }
    }

    public Task LeaveAsync()
    {
        if (leaveTask != null && !leaveTask.IsCompleted) return leaveTask;
        return leaveTask = LeaveInternalAsync();
    }

    private async Task LeaveInternalAsync()
    {
        leaving = true;
        operation?.Cancel();
        await activeOperation;
        await ClearRoomAsync();
        if (manager != null && manager.IsListening) manager.Shutdown();
        matchStarted = false;
        leaving = false;
        nextRefresh = 0;
        SetStatus("Choose a game or host your own.");
    }

    private async Task ClearRoomAsync()
    {
        var room = Room;
        Room = null;
        if (room == null) return;
        try
        {
            if (room.HostId == AuthenticationService.Instance.PlayerId)
                await LobbyService.Instance.DeleteLobbyAsync(room.Id);
            else await LobbyService.Instance.RemovePlayerAsync(room.Id, AuthenticationService.Instance.PlayerId);
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound ||
            e.Reason == LobbyExceptionReason.PlayerNotFound) { }
        catch (Exception) { Debug.LogWarning("[Lobby] Room cleanup could not reach the service; the listing will expire."); }
    }

    private async Task MaintainAsync(bool heartbeat)
    {
        maintaining = true;
        var room = Room;
        nextHeartbeat = heartbeat ? Time.realtimeSinceStartup + 15f : nextHeartbeat;
        nextPoll = Time.realtimeSinceStartup + 4f;
        try
        {
            if (heartbeat) await LobbyService.Instance.SendHeartbeatPingAsync(room.Id);
            else
            {
                var updated = await LobbyService.Instance.GetLobbyAsync(room.Id);
                if (Room?.Id == room.Id && !matchStarted && !leaving) Room = updated;
            }
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
        {
            if (!matchStarted && !leaving && Room?.Id == room.Id)
            {
                await LeaveAsync();
                SetStatus("The host closed this game. Choose another game.");
            }
        }
        catch (Exception) { if (!matchStarted && !leaving) SetStatus("Connection interrupted. Retrying..."); }
        finally { maintaining = false; Changed?.Invoke(); }
    }

    private void OnConnected(ulong clientId)
    {
        if (manager == null || Room == null) return;
        if (manager.IsServer && manager.ConnectedClientsIds.Count >= 2) _ = CloseListingAsync();
        else if (!manager.IsServer && clientId == manager.LocalClientId) matchStarted = true;
    }

    private async Task CloseListingAsync()
    {
        if (closingListing) return;
        closingListing = true;
        matchStarted = true;
        await ClearRoomAsync();
        closingListing = false;
    }

    private async void OnDisconnected(ulong clientId)
    {
        if (leaving || Busy || manager == null) return;
        if (clientId == manager.LocalClientId)
        {
            await LeaveAsync();
            SetStatus("Disconnected. Refresh the list and try again.");
        }
    }

    private async void OnTransportFailure()
    {
        if (leaving) return;
        await LeaveAsync();
        SetStatus("Network connection failed. Check your internet and try again.");
    }

    private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu") await LeaveAsync();
    }

    private void SetStatus(string text) { Status = text; Changed?.Invoke(); }

    public static string FriendlyError(Exception error)
    {
        if (error is OperationCanceledException) return "Connection timed out or was canceled. Please try again.";
        if (error is LobbyServiceException lobby)
        {
            switch (lobby.Reason)
            {
                case LobbyExceptionReason.IncorrectPassword: return "Incorrect password. Please try again.";
                case LobbyExceptionReason.LobbyFull: return "Someone joined first. This game is full.";
                case LobbyExceptionReason.LobbyLocked:
                case LobbyExceptionReason.LobbyNotFound: return "This game is no longer available. Refresh the list.";
                case LobbyExceptionReason.RateLimited: return "Please wait a moment before trying again.";
                case LobbyExceptionReason.Forbidden: return "Lobby access is unavailable. Check the project's Unity Gaming Services configuration.";
            }
            return "Could not reach the game lobby. Check your internet and try again.";
        }
        return error is ArgumentException || error is InvalidOperationException
            ? error.Message : "Connection failed. Check your internet and try again.";
    }

    private void OnApplicationQuit()
    {
        operation?.Cancel();
        _ = ClearRoomAsync(); // Best effort; lost hosts also disappear when their heartbeat expires.
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeNetwork();
        operation?.Cancel();
        if (Instance == this) Instance = null;
    }
}
