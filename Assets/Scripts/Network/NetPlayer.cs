using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using System.Collections;
public class NetPlayer : NetworkBehaviour
{
    public NetworkSpellKind SelectedMageSpell { get; set; }
    public static NetPlayer Local; // convenience
    public NetworkVariable<FixedString128Bytes> PlayerName = new NetworkVariable<FixedString128Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString128Bytes> MageCardName = new NetworkVariable<FixedString128Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<TeamColor> Side = new NetworkVariable<TeamColor>();
    public NetworkVariable<int> SpellCount = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Owner,
    NetworkVariableWritePermission.Server
);

    public NetworkVariable<bool> MageAbilityAvailable = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server
    );
    // AUTO-FLIP BLACK CLIENT
    private bool automaticBlackFlipApplied = false;
    private Coroutine autoFlipRoutine;
    public override void OnNetworkSpawn()
    {
        // A player can spawn after GameState resets the match. Only accepted picks grant spells.
        if (IsServer) SpellCount.Value = 0;

        if (IsOwner)
        {
            Local = this;
            string displayName = LobbyIdentity.PlayerName;
            PlayerName.Value = new FixedString128Bytes(
                string.IsNullOrEmpty(displayName) ? "Player" : displayName);
            CardDrawer.Instance?.SyncSpellCountToNetwork();

            // NEW: react when this player receives its side
            Side.OnValueChanged += HandleSideChanged;

            // NEW: also check immediately in case side is already assigned
            TryApplyAutomaticBoardOrientation(Side.Value);
        }

        if (IsServer)
        {
            // host gets White; first non-host gets Black
            bool whiteTaken = false;
            foreach (var p in FindObjectsByType<NetPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (p != this && p.Side.Value == TeamColor.White) whiteTaken = true;

            Side.Value = whiteTaken ? TeamColor.Black : TeamColor.White;
        }
    }

    public bool CanAct() => !CardDrawer.BlocksPlay && IsOwner && GameState.Instance && GameState.Instance.IsSpawned &&
        ChessBoard.Instance != null && !ChessBoard.Instance.gameOver &&
        GameState.Instance.IsMyTurn(Side.Value);

    // Called by your input/UI instead of moving locally
    public void TryRequestMove(int pieceId, Vector2Int to)
    {
        if (!CanAct()) return;
        GameState.Instance.RequestMoveServerRpc(pieceId, to.x, to.y);
    }

    public void EndTurn()
    {
        if (!CanAct()) return;
        GameState.Instance.EndTurnServerRpc();
    }

    public static NetPlayer FindByClient(ulong clientId)
    {
        foreach (var nb in FindObjectsByType<NetPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (nb.OwnerClientId == clientId) return nb;
        return null;
    }
    // NEW
    private void HandleSideChanged(
        TeamColor oldSide,
        TeamColor newSide)
    {
        if (!IsOwner) return;

        TryApplyAutomaticBoardOrientation(newSide);
    }// NEW
    private void TryApplyAutomaticBoardOrientation(TeamColor side)
    {
        // Only Black starts flipped
        if (side != TeamColor.Black)
            return;

        // Do it only once
        if (automaticBlackFlipApplied)
            return;

        automaticBlackFlipApplied = true;

        autoFlipRoutine =
            StartCoroutine(ApplyBlackOrientationWhenReady());
    }
    // NEW
    private IEnumerator ApplyBlackOrientationWhenReady()
    {
        // Wait until board flip controller exists
        while (BoardFlipController.Instance == null)
            yield return null;

        BoardFlipController.Instance.SetFlipped(true);

        autoFlipRoutine = null;
    }
    // NEW
    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Side.OnValueChanged -= HandleSideChanged;

            if (Local == this)
                Local = null;
        }

        base.OnNetworkDespawn();
    }
    [ServerRpc]
    public void SetMageAbilityAvailableServerRpc(bool available, int kind = -1)
    {
        // Selection initializes the resource once. Only accepted server actions spend it.
        GameState.Instance?.InitializeMage(this, available, kind);
    }

    public void AddSpellServer(int amount)
    {
        if (!IsServer) return;
        SpellCount.Value = Mathf.Max(0, SpellCount.Value + amount);
    }

    public static NetPlayer FindBySide(TeamColor side)
    {
        foreach (NetPlayer player in FindObjectsByType<NetPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (player.Side.Value == side) return player;

        return null;
    }
}
