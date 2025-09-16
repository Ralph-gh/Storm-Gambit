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
        
        var sender = p.Receive.SenderClientId;
        var player = NetPlayer.FindByClient(sender);
        if (player == null || player.Side.Value != CurrentTurn.Value)
        {
            Debug.Log($"[SRPC] not your turn | sender={sender} side={player?.Side.Value} turn={CurrentTurn.Value}");
            return;
        }

        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) { Debug.Log($"[SRPC] no piece id={pieceId}"); return; }

        ChessBoard.Instance.EnsureBoardEntry(piece);   // <- add this line

        Vector2Int to = new Vector2Int(targetX, targetY);
        if (!ChessBoard.Instance.IsInsideBoard(to)) { Debug.Log($"[SRPC] outside {to}"); return; }

        //  log what server sees on target before legality
        var victim = ChessBoard.Instance.GetPieceAt(to);
        Debug.Log($"[SRPC] check {piece.pieceType}#{piece.Id} {piece.team}  {to} | victim={(victim ? victim.pieceType.ToString() : "null")}");

        if (victim != null && victim.team == piece.team) { Debug.Log("[SRPC] own piece on target"); return; }

        // Validate chess rules (pawn-diagonal depends on victim != null)
        bool legal = ChessBoard.Instance.IsLegalMove(piece, to);
        if (!legal)
        {
            if (piece.pieceType == PieceType.Pawn)
                Debug.Log($"[SRPC] illegal (pawn) to={to} victim={(victim ? victim.pieceType.ToString() : "null")} currentCell={piece.currentCell}");
            else
                Debug.Log($"[SRPC] illegal ({piece.pieceType}) to={to} currentCell={piece.currentCell}");
            return;
        }

        int capturedId = -1;
        if (victim != null)
        {
            capturedId = victim.Id;
            Debug.Log($"[SRPC] CAPTURE {victim.pieceType}#{victim.Id} at {to}");
            ChessBoard.Instance.CapturePiece(to);
        }

        // For diagnostics: log board “from” and “to”
        var from = piece.currentCell;
        Debug.Log($"[SRPC] MOVE {piece.pieceType}#{piece.Id} {from} {to}");

        ChessBoard.Instance.ExecuteMoveServer(piece, to);

        ApplyMoveClientRpc(piece.Id, to.x, to.y, capturedId);

        var next = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        CurrentTurn.Value = next;
        Debug.Log($"[SRPC] Turn  {next}");
    }

    [ClientRpc]
    void ApplyMoveClientRpc(int moverId, int toX, int toY, int capturedId)
    {
        string role = Unity.Netcode.NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT";
        Debug.Log($"[RPC/{role}] mover={moverId} to=({toX},{toY}) captured={capturedId}");

        var mover = ChessBoard.Instance.GetPieceById(moverId);
        if (mover == null)
        {
            Debug.LogWarning($"[RPC/{role}] mover {moverId} missing  rebuilding index");
            ChessBoard.Instance.RebuildIndexFromScene();
            mover = ChessBoard.Instance.GetPieceById(moverId);
            if (mover == null) { Debug.LogWarning($"[RPC/{role}] mover {moverId} STILL missing"); return; }
        }

        if (capturedId >= 0)
        {
            var victim = ChessBoard.Instance.GetPieceById(capturedId);
            if (victim != null)
            {
                Debug.Log($"[RPC/{role}] removing victim id={capturedId} at {victim.currentCell}");
                ChessBoard.Instance.RemovePieceLocal(victim);
            }
            else
            {
                Debug.LogWarning($"[RPC/{role}] victim {capturedId} not found (already removed?)");
            }
        }

        ChessBoard.Instance.MovePieceLocal(mover, new Vector2Int(toX, toY));
        TurnManager.Instance?.SyncTurn(CurrentTurn.Value);
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
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        //sync turn
        if (TurnManager.Instance)TurnManager.Instance.SyncTurn(CurrentTurn.Value);
        CurrentTurn.OnValueChanged += (oldV, NewV) =>
        {
            if (TurnManager.Instance) TurnManager.Instance.SyncTurn(NewV);
        };
        if (IsServer) StartCoroutine(BoardHeartbeat());
    }

    private System.Collections.IEnumerator BoardHeartbeat()
    {
        // small delay to let initializers finish
        yield return null;

        var board = ChessBoard.Instance;
        var wait = new WaitForSeconds(0.5f);   // tune to taste

        while (Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            board.RebuildBoardAndIndexFromScene();
            yield return wait;
        }
    }
    // --- SPELL RPCs ---

    [ServerRpc(RequireOwnership = false)]
    public void TeleportPieceServerRpc(int pieceId, int x, int y, ServerRpcParams p = default)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) { Debug.Log($"[SRPC] Teleport: no piece {pieceId}"); return; }

        var to = new Vector2Int(x, y);
        if (!ChessBoard.Instance.IsInsideBoard(to)) return;
        if (ChessBoard.Instance.GetPieceAt(to) != null) return; // must be empty per your UI flow

        // Server: apply teleport (board + piece)
        ChessBoard.Instance.MovePiece(piece.currentCell, to);
        piece.SetPosition(to, BoardInitializer.Instance.GetWorldPosition(to));
        piece.hasMoved = true;

        TeleportPieceClientRpc(pieceId, x, y);
    }

    [ClientRpc]
    void TeleportPieceClientRpc(int pieceId, int x, int y)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) { ChessBoard.Instance.RebuildIndexFromScene(); piece = ChessBoard.Instance.GetPieceById(pieceId); }
        if (piece == null) return;

        var to = new Vector2Int(x, y);
        ChessBoard.Instance.MovePieceLocal(piece, to);
        piece.SetPosition(to, BoardInitializer.Instance.GetWorldPosition(to));
        piece.hasMoved = true;
    }

    // Apply Divine Protection on all clients for the selected piece
    [ServerRpc(RequireOwnership = false)]
    public void ApplyDivineProtectionServerRpc(int pieceId, ServerRpcParams p = default)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) return;

        ApplyDivineProtectionClientRpc(pieceId);
    }

    [ClientRpc]
    void ApplyDivineProtectionClientRpc(int pieceId)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece != null) piece.ApplyDivineProtectionOneTurn(); // spawns sphere & hooks turn listener
    }

    // Resurrect by type/team at a server-chosen spawn square
    [ServerRpc(RequireOwnership = false)]
    public void ResurrectServerRpc(TeamColor team, PieceType pieceType, int spawnX, int spawnY, ServerRpcParams p = default)
    {
        var spawn = new Vector2Int(spawnX, spawnY);
        if (!ChessBoard.Instance.IsInsideBoard(spawn)) return;
        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);
        if (spawn.x == -1) return;

        // Instantiate from your prefab table (implement this helper so server & clients match)
        var prefab = BoardInitializer.Instance.GetPrefab(team, pieceType);
        if (prefab == null) { Debug.LogWarning($"No prefab for {team} {pieceType}"); return; }

        var go = Instantiate(prefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        var newPiece = go.GetComponent<ChessPiece>();
        newPiece.team = team;
        newPiece.pieceType = pieceType;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        newPiece.MarkAsResurrected();

        ChessBoard.Instance.PlacePiece(newPiece, spawn);
        // server removes from graveyard too (see section C)

        ResurrectClientRpc(team, pieceType, spawnX, spawnY);
    }

    [ClientRpc]
    void ResurrectClientRpc(TeamColor team, PieceType pieceType, int spawnX, int spawnY)
    {
        var spawn = new Vector2Int(spawnX, spawnY);
        var prefab = BoardInitializer.Instance.GetPrefab(team, pieceType);
        if (prefab == null) return;

        var go = Instantiate(prefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        var p = go.GetComponent<ChessPiece>();
        p.team = team;
        p.pieceType = pieceType;
        p.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        p.MarkAsResurrected();

        ChessBoard.Instance.PlacePiece(p, spawn);
        // client removes from graveyard too (see section C)
    }
}
