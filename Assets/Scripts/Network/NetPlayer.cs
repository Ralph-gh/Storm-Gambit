using Unity.Netcode;
using UnityEngine;

public class NetPlayer : NetworkBehaviour
{
    public static NetPlayer Local; // convenience
    public NetworkVariable<TeamColor> Side = new NetworkVariable<TeamColor>();

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;

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
}
