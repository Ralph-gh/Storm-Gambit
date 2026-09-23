using Unity.Netcode;
using UnityEngine;
using System.Collections;
public class NetPlayer : NetworkBehaviour
{
    public static NetPlayer Local; // convenience
    public NetworkVariable<TeamColor> Side = new NetworkVariable<TeamColor>();
    // AUTO-FLIP BLACK CLIENT
    private bool automaticBlackFlipApplied = false;
    private Coroutine autoFlipRoutine;
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Local = this;

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

    public bool CanAct() => IsOwner && GameState.Instance && GameState.Instance.IsMyTurn(Side.Value);

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
}
