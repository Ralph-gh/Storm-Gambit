using System;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;

public class LobbyBrowserUI : MonoBehaviour
{
    public LobbySceneController controller;
    [HideInInspector] public LobbyMenuNavigation navigation;
    public TMP_InputField playerNameInput;
    public TMP_InputField roomNameInput;
    public TMP_InputField hostPasswordInput;
    public Toggle passwordToggle;
    public UIButton browseTab, hostTab, refreshButton, createButton, leaveButton, backButton;
    public UIButton passwordJoinButton, passwordCancelButton;
    public UIButton editNameButton, saveNameButton, emptyHostButton, retryButton;
    public GameObject browsePanel, hostPanel, waitingPanel, passwordDialog;
    public GameObject identitySummary, identityEditor, feedbackPanel, emptyState;
    public RectTransform gamePanels;
    public TMP_InputField joinPasswordInput;
    public TMP_Text playerNameLabel, emptyTitle;
    public TMP_Text dialogTitle, dialogStatus, statusText, gameCountText, emptyText;
    public TMP_Text waitingTitle, waitingDetails;
    public RectTransform rows;
    public LobbyRoomRow rowTemplate;

    private LobbyMatchmaking service;
    private bool hostingTab;
    private bool editingName;
    private bool bound;
    private bool initialized;
    private bool returning;
    private string localFeedback;
    private Lobby selectedGame;
    private IReadOnlyList<Lobby> renderedGames;
    private readonly List<LobbyRoomRow> activeRows = new List<LobbyRoomRow>();

    private void Start()
    {
        service = LobbyMatchmaking.GetOrCreate();
        playerNameInput.text = LobbyIdentity.PlayerName;
        editingName = string.IsNullOrWhiteSpace(playerNameInput.text);
        browseTab.onClick.AddListener(() => SelectTab(false));
        hostTab.onClick.AddListener(() => SelectTab(true));
        refreshButton.onClick.AddListener(RefreshGames);
        retryButton.onClick.AddListener(RefreshGames);
        emptyHostButton.onClick.AddListener(() => SelectTab(true));
        editNameButton.onClick.AddListener(BeginNameEdit);
        saveNameButton.onClick.AddListener(SavePlayerName);
        playerNameInput.onSubmit.AddListener(_ => SavePlayerName());
        createButton.onClick.AddListener(CreateGame);
        leaveButton.onClick.AddListener(LeaveGame);
        backButton.onClick.AddListener(ReturnToLobby);
        passwordJoinButton.onClick.AddListener(JoinSelectedGame);
        passwordCancelButton.onClick.AddListener(ClosePasswordDialog);
        passwordToggle.onValueChanged.AddListener(value =>
        {
            hostPasswordInput.interactable = value;
            if (!value) hostPasswordInput.text = "";
        });
        hostPasswordInput.interactable = passwordToggle.isOn;
        initialized = true;
        Bind();
        Render();
    }

    private void OnEnable()
    {
        if (initialized) { Bind(); Render(); }
    }

    private void Bind()
    {
        if (bound) return;
        service.Changed += Render;
        controller.StatusChanged += ShowNetworkStatus;
        service.AttachBrowser();
        bound = true;
    }

    private void Update()
    {
        if (service == null) return;
        refreshButton.interactable = service.CanManualRefresh;
        retryButton.interactable = service.CanManualRefresh;
    }

    private void OnDisable()
    {
        if (!bound) return;
        service.Changed -= Render;
        service.DetachBrowser();
        controller.StatusChanged -= ShowNetworkStatus;
        bound = false;
    }

    public void BeginNameEdit()
    {
        editingName = true;
        localFeedback = null;
        ShowIdentity();
        ShowFeedback(null);
        playerNameInput.ActivateInputField();
    }

    public void SavePlayerName()
    {
        TrySaveName();
        Render();
    }

    private bool TrySaveName()
    {
        try
        {
            LobbyIdentity.SaveName(playerNameInput.text);
            playerNameInput.text = LobbyIdentity.PlayerName;
            editingName = false;
            localFeedback = null;
            playerNameInput.DeactivateInputField();
            ShowIdentity();
            ShowFeedback(null);
            return true;
        }
        catch (ArgumentException error)
        {
            editingName = true;
            localFeedback = error.Message;
            ShowIdentity();
            ShowFeedback(localFeedback);
            playerNameInput.ActivateInputField();
            return false;
        }
    }

    private void ShowIdentity()
    {
        identityEditor.SetActive(editingName);
        identitySummary.SetActive(!editingName);
        playerNameLabel.text = playerNameInput.text;
    }

    public void SelectTab(bool host)
    {
        hostingTab = host;
        localFeedback = null;
        ClosePasswordDialog();
        browsePanel.SetActive(!host);
        hostPanel.SetActive(host);
        waitingPanel.SetActive(false);
        LobbyUIBuilder.SetTabSelected(browseTab, !host);
        LobbyUIBuilder.SetTabSelected(hostTab, host);
        Render();
    }

    public async void ReturnToLobby()
    {
        if (returning) return;
        if (navigation == null) { controller.BackToMainMenu(); return; }
        returning = true;
        backButton.interactable = false;
        try
        {
            if (service != null) await service.LeaveAsync();
            if (this == null) return;
            ClosePasswordDialog();
            localFeedback = null;
            navigation.ShowLanding();
        }
        finally
        {
            if (this != null) { returning = false; backButton.interactable = true; }
        }
    }

    private void RefreshGames()
    {
        localFeedback = null;
        _ = service.RefreshAsync(true);
    }

    private async void CreateGame()
    {
        if (!TrySaveName()) return;
        ClosePasswordDialog();
        await service.HostAsync(playerNameInput.text, roomNameInput.text, hostPasswordInput.text, passwordToggle.isOn);
        if (this == null) return;
        hostPasswordInput.text = "";
        if (service.Room == null && !NetworkConnected()) localFeedback = service.Status;
        Render();
    }

    private async void LeaveGame()
    {
        leaveButton.interactable = false;
        await service.LeaveAsync();
        if (this == null) return;
        hostingTab = false;
        localFeedback = null;
        Render();
    }

    public void SelectGame(Lobby game)
    {
        if (service == null || service.Busy || service.Room != null || !TrySaveName()) return;
        selectedGame = game;
        if (game.HasPassword)
        {
            dialogTitle.text = game.Name;
            dialogStatus.text = "Enter the host's password to join.";
            joinPasswordInput.text = "";
            passwordDialog.SetActive(true);
            ShowFeedback(null);
            joinPasswordInput.ActivateInputField();
        }
        else JoinSelectedGame();
    }

    private async void JoinSelectedGame()
    {
        if (selectedGame == null || service.Busy) return;
        localFeedback = null;
        await service.JoinAsync(selectedGame, playerNameInput.text, joinPasswordInput.text);
        if (this == null) return;
        if (service.Room != null || NetworkConnected()) ClosePasswordDialog();
        else
        {
            dialogStatus.text = service.Status;
            if (!passwordDialog.activeSelf) localFeedback = service.Status;
        }
        Render();
    }

    private static bool NetworkConnected() => Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsConnectedClient;

    private void ClosePasswordDialog()
    {
        passwordDialog.SetActive(false);
        joinPasswordInput.text = "";
    }

    private void ShowNetworkStatus(string message)
    {
        if (message.StartsWith("Disconnected") || message.StartsWith("Network connection failed") || message.StartsWith("Connection timed out"))
            localFeedback = message;
        if (service.Room != null || NetworkConnected() || localFeedback != null) ShowFeedback(message);
    }

    public void ShowFeedback(string message)
    {
        bool show = !string.IsNullOrWhiteSpace(message) && !passwordDialog.activeSelf;
        feedbackPanel.SetActive(show);
        statusText.text = message ?? "";
        var offset = gamePanels.offsetMin;
        offset.y = show ? 156 : 28;
        gamePanels.offsetMin = offset;
    }

    private void Render()
    {
        if (this == null || service == null) return;
        bool waiting = service.Room != null;
        bool editable = !service.Busy && !waiting;
        browsePanel.SetActive(!waiting && !hostingTab);
        hostPanel.SetActive(!waiting && hostingTab);
        waitingPanel.SetActive(waiting);
        browseTab.interactable = hostTab.interactable = editable;
        LobbyUIBuilder.SetTabSelected(browseTab, !hostingTab);
        LobbyUIBuilder.SetTabSelected(hostTab, hostingTab);
        createButton.interactable = editable;
        playerNameInput.interactable = editable;
        saveNameButton.interactable = editNameButton.interactable = editable;
        roomNameInput.interactable = editable;
        passwordToggle.interactable = editable;
        hostPasswordInput.interactable = editable && passwordToggle.isOn;
        refreshButton.interactable = retryButton.interactable = service.CanManualRefresh;
        leaveButton.interactable = !service.Busy;
        passwordJoinButton.interactable = passwordCancelButton.interactable = !service.Busy;
        if (passwordDialog.activeSelf && service.Busy) dialogStatus.text = service.Status;
        emptyHostButton.interactable = editable;
        ShowIdentity();
        ShowFeedback(service.Busy ? service.Status : localFeedback);
        if (waiting)
        {
            waitingTitle.text = service.Room.Name;
            string opponent = "Waiting for an opponent...";
            foreach (var player in service.Room.Players)
            {
                if (player.Id == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId) continue;
                if (player.Data != null && player.Data.TryGetValue(LobbyMatchmaking.NameKey, out var value)) opponent = value.Value;
            }
            waitingDetails.text = "YOU\n" + LobbyIdentity.PlayerName + "\n\nOPPONENT\n" + opponent + "\n\n" +
                (service.Room.HasPassword ? "Password protected" : "Open game") + "\nThe match starts when both players connect.";
        }
        PresentGames(service.Games, service.Refreshing, service.HasQueried, service.LastQueryError);
        foreach (var row in activeRows) if (row != null) row.joinButton.interactable = editable;
    }

    // Also used by the editor preview and regression checks; no service requests are made here.
    public void PresentGames(IReadOnlyList<Lobby> games, bool refreshing, bool hasQueried, string error)
    {
        if (renderedGames != games)
        {
            renderedGames = games;
            foreach (var row in activeRows)
                if (row != null)
                {
                    if (Application.isPlaying) Destroy(row.gameObject);
                    else DestroyImmediate(row.gameObject);
                }
            activeRows.Clear();
            foreach (var room in games)
            {
                var row = Instantiate(rowTemplate, rows);
                row.gameObject.SetActive(true);
                row.Bind(room, SelectGame);
                activeRows.Add(row);
            }
        }
        bool failed = !string.IsNullOrEmpty(error);
        bool loading = refreshing || !hasQueried;
        gameCountText.text = refreshing ? "Updating game list..." : games.Count + (games.Count == 1 ? " game available" : " games available");
        emptyState.SetActive(games.Count == 0 || failed);
        rows.gameObject.SetActive(games.Count > 0 && !failed);
        emptyTitle.text = failed ? "Couldn't load games" : loading ? "Finding games..." : "No games available";
        emptyText.text = failed ? error : loading ? "Checking for open games. You can also host your own." : "Host a game and let your next rival find you.";
        retryButton.gameObject.SetActive(failed);
        emptyHostButton.gameObject.SetActive(!failed);
    }
}
