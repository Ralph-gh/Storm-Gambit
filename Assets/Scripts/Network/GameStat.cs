using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    // Which side can act
    public NetworkVariable<TeamColor> CurrentTurn = new NetworkVariable<TeamColor>(TeamColor.White);

    // Quick piece addressing: your ChessPiece must have a stable unique Id.
    // If you don't have it yet, add `public int Id;` to ChessPiece and assign in BoardInitializer.
    // We'll use RPCs to move pieces by Id to avoid full-state replication for now.

    void Awake() => Instance = this;

    public bool IsMyTurn(TeamColor mySide) => CurrentTurn.Value == mySide;

    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(int pieceId, int targetX, int targetY, ServerRpcParams p = default)
    {
        var senderClientId = p.Receive.SenderClientId;
        var netPlayer = NetPlayer.FindByClient(senderClientId);
        if (netPlayer == null) return;
        if (netPlayer.Side.Value != CurrentTurn.Value) return; // not your turn

        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) return;

        Vector2Int from = piece.currentCell;
        Vector2Int to = new Vector2Int(targetX, targetY);

        if (!ChessBoard.Instance.IsInsideBoard(to)) return;
        if (!ChessBoard.Instance.IsLegalMove(piece, to)) return;

        var capture = ChessBoard.Instance.GetPieceAt(to);
        if (capture != null && capture.team == piece.team) return;

        ChessBoard.Instance.ExecuteMoveServer(piece, to);

        ApplyMoveClientRpc(piece.Id, from.x, from.y, to.x, to.y, capture ? capture.Id : -1);

        CurrentTurn.Value = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
    }

    [ClientRpc]
    private void ApplyMoveClientRpc(int pieceId, int fromX, int fromY, int toX, int toY, int capturedId)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) return;

        if (capturedId >= 0)
        {
            var dead = ChessBoard.Instance.GetPieceById(capturedId);
            if (dead) ChessBoard.Instance.RemovePieceLocal(dead);
        }

        ChessBoard.Instance.MovePieceLocal(piece, new Vector2Int(toX, toY));
        var tm = TurnManager.Instance;
        if (tm != null) tm.SyncTurn(CurrentTurn.Value);
    }

    // Optional: end turn without moving (e.g., skip/cast-only turn)
    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams p = default)
    {
        var senderClientId = p.Receive.SenderClientId;
        var netPlayer = NetPlayer.FindByClient(senderClientId);
        if (netPlayer == null) return;
        if (netPlayer.Side.Value != CurrentTurn.Value) return;
        CurrentTurn.Value = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
    }
}
