using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    // Which side can act
    public NetworkVariable<TeamColor> CurrentTurn = new NetworkVariable<TeamColor>(TeamColor.White);

    // Quick piece addressing: ChessPiece must have a stable unique Id.
    // If you don't have it yet, add `public int Id;` to ChessPiece and assign in BoardInitializer.
    // We'll use RPCs to move pieces by Id to avoid full-state replication for now.
    public NetworkVariable<int> MoveNumber = new NetworkVariable<int>(0);//used for turn counter in network play
    void Awake() => Instance = this;
    

    public bool IsMyTurn(TeamColor mySide) => CurrentTurn.Value == mySide;

    [ServerRpc(RequireOwnership = false)]

    public void RequestMoveServerRpc(int pieceId, int targetX, int targetY, ServerRpcParams p = default)
    {
        // Always ensure the server-side board/index are fresh before any legality checks
        ChessBoard.Instance.RebuildBoardAndIndexFromScene();
        var sender = p.Receive.SenderClientId;
        var player = NetPlayer.FindByClient(sender);
        if (player == null || player.Side.Value != CurrentTurn.Value)
        {
            Debug.Log($"[SRPC] not your turn | sender={sender} side={player?.Side.Value} turn={CurrentTurn.Value}");
            return;
        }

        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) { Debug.Log($"[SRPC] no piece id={pieceId}"); return; }

        TeamColor next = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        //ChessBoard.Instance.EnsureBoardEntry(piece);  
        if (next == TeamColor.Black)
        {
            // White just moved. If this is the very first move, set to 1.
            if (MoveNumber.Value == 0) MoveNumber.Value = 1;
        }
        else // next == TeamColor.White
        {
            // Black just moved. A full move has completed; increment (PGN-style).
            if (MoveNumber.Value > 0) MoveNumber.Value += 1;

            // Every 10 full moves, on White’s turn, both players draw 1 spell
            if (MoveNumber.Value > 0 && MoveNumber.Value % 10 == 0)
                DrawSpellForBothPlayersClientRpc();
        }

        Vector2Int to = new Vector2Int(targetX, targetY);
        if (!ChessBoard.Instance.IsInsideBoard(to)) { Debug.Log($"[SRPC] outside {to}"); return; }

        bool IsCastleAttempt() =>
        piece.pieceType == PieceType.King &&
        piece.currentCell.y == to.y &&
        Mathf.Abs(to.x - piece.currentCell.x) == 2;

        if (IsCastleAttempt())
        {
            // Mirror your King.IsValidMove tests to see what’s failing
            var king = ChessBoard.Instance.GetPieceAt(piece.currentCell);
            bool kingOk = (king != null && !king.hasMoved);

            bool kingSide = (to.x > piece.currentCell.x);
            int rookX = kingSide ? 7 : 0;
            var rookPos = new Vector2Int(rookX, piece.currentCell.y);
            var rook = ChessBoard.Instance.GetPieceAt(rookPos);
            bool rookOk = (rook != null && rook.pieceType == PieceType.Rook && rook.team == piece.team && !rook.hasMoved);

            bool targetEmpty = ChessBoard.Instance.GetPieceAt(to) == null;

            // Path empty (excludes rook square)
            bool pathEmpty = true;
            int step = kingSide ? 1 : -1;
            for (int x = piece.currentCell.x + step; x != rookPos.x; x += step)
            {
                if (ChessBoard.Instance.GetPieceAt(new Vector2Int(x, piece.currentCell.y)) != null)
                {
                    pathEmpty = false; break;
                }
            }

            Debug.Log($"[SRPC/CASTLE DIAG] from={piece.currentCell} to={to} side={piece.team} " +
                      $"kingOk={kingOk} rookOk={rookOk} targetEmpty={targetEmpty} pathEmpty={pathEmpty}");
        }
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
        bool enPassantCapture = false;
        Vector2Int from = piece.currentCell;  // you'll log later; we need it now

        // EP if: moving pawn diagonally into empty square that equals enPassantTarget
        if (piece.pieceType == PieceType.Pawn &&
            ChessBoard.Instance.enPassantTarget.x >= 0 &&
            victim == null &&                               // target square is empty
            Mathf.Abs(to.x - from.x) == 1 &&
            (to.y - from.y) == ((piece.team == TeamColor.White) ? 1 : -1) &&
            to == ChessBoard.Instance.enPassantTarget)
        {
            enPassantCapture = true;
        }
        int capturedId = -1;

        if (enPassantCapture)
        {
            // The victim pawn is on the from-rank, same file as 'to'
            var victimCell = new Vector2Int(to.x, from.y);
            var epVictim = ChessBoard.Instance.GetPieceAt(victimCell);

            if (epVictim == null || epVictim.pieceType != PieceType.Pawn || epVictim.team == piece.team)
            {
                Debug.Log("[SRPC] EP victim missing or invalid");
                return;
            }
            if (epVictim.IsDivinelyProtected)
            {
                Debug.Log("[SRPC] EP blocked: victim divinely protected");
                return;
            }

            capturedId = epVictim.Id;
            Debug.Log($"[SRPC] EN PASSANT CAPTURE Pawn#{capturedId} at {victimCell}");
            ChessBoard.Instance.CapturePiece(victimCell); // note: capture the pawn at its square
        }
        else if (victim != null)
        {
            if (victim.IsDivinelyProtected)
            {
                Debug.Log("[SRPC] capture blocked: target divinely protected");
                return;
            }
            capturedId = victim.Id;
            Debug.Log($"[SRPC] CAPTURE {victim.pieceType}#{victim.Id} at {to}");
            ChessBoard.Instance.CapturePiece(to);
        }

        // For diagnostics: log board “from” and “to”
        //var from = piece.currentCell;
        Debug.Log($"[SRPC] MOVE {piece.pieceType}#{piece.Id} {from} {to}");

        // after legality checks and optional victim capture...
        int rookId = -1, rookToX = 0, rookToY = 0;

        // Detect castling (king moved two squares horizontally)
        if (piece.pieceType == PieceType.King && Mathf.Abs(to.x - from.x) == 2 && from.y == to.y)
        {
            bool isKingSide = (to.x > from.x);
            var rookFrom = new Vector2Int(isKingSide ? 7 : 0, from.y);
            var rook = ChessBoard.Instance.GetPieceAt(rookFrom);
            if (rook != null && rook.pieceType == PieceType.Rook && rook.team == piece.team)
            {
                rookId = rook.Id;
                var rookTo = new Vector2Int(isKingSide ? to.x - 1 : to.x + 1, from.y);

                // Move rook on server
                ChessBoard.Instance.ExecuteMoveServer(rook, rookTo);
                rookToX = rookTo.x; rookToY = rookTo.y;
            }
        }

        // Move king on server
        ChessBoard.Instance.ExecuteMoveServer(piece, to);

        // Notify clients: king move + optional castle rook move
      
        

        
        // ===== En Passant window maintenance =====
        int epX = -1, epY = -1, epPawnId = -1;
        ChessBoard.Instance.ClearEnPassant();
        if (piece.pieceType == PieceType.Pawn && Mathf.Abs(to.y - from.y) == 2 && to.x == from.x)
        {
            var mid = new Vector2Int(from.x, (from.y + to.y) / 2); // passed-over square
            ChessBoard.Instance.enPassantTarget = mid;
            ChessBoard.Instance.enPassantPawnId = piece.Id;
            epX = mid.x; epY = mid.y; epPawnId = piece.Id;
        }
        // Send the move
        ApplyMoveClientRpc(piece.Id, to.x, to.y, capturedId);
        if (rookId >= 0)
            MoveRookClientRpc(rookId, rookToX, rookToY);
        // Send the EP window for the NEXT move
        SetEnPassantClientRpc(epX, epY, epPawnId);
        // flip turn...
        //var next = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        CurrentTurn.Value = next;
    }
    [ClientRpc]
    void SetEnPassantClientRpc(int epX, int epY, int epPawnId)
    {
        ChessBoard.Instance.ClearEnPassant();
        if (epX >= 0)
        {
            ChessBoard.Instance.enPassantTarget = new Vector2Int(epX, epY);
            ChessBoard.Instance.enPassantPawnId = epPawnId;
        }
    }
    [ClientRpc]
    void DrawSpellForBothPlayersClientRpc()
    {
        // Each client draws into their own hand (1 spell card)
        var drawers = FindObjectsByType<CardDrawer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var d in drawers) d.DrawOneSpellCard();  // see CardDrawer update below

        // Optional: UI toast, VFX, SFX — leave to your UX layer.
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

                // Add to local graveyard and notify listeners (enables Resurrection card)
                ChessBoard.Instance.AddCapturedPiece(victim);
                ChessBoard.Instance.RaiseGraveyardChanged();

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
        ChessBoard.Instance.ClearEnPassant();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        #if UNITY_SERVER || UNITY_EDITOR
        foreach (var p in GameObject.FindObjectsOfType<ChessPiece>())
        {
            if (p.pieceType == PieceType.King || p.pieceType == PieceType.Rook)
                p.hasMoved = false;
        }
        ChessBoard.Instance.RebuildBoardAndIndexFromScene();
        #endif
        //sync turn
        if (TurnManager.Instance)TurnManager.Instance.SyncTurn(CurrentTurn.Value);
        CurrentTurn.OnValueChanged += (oldV, NewV) =>
        {
            if (TurnManager.Instance) TurnManager.Instance.SyncTurn(NewV);
        };
        {
            base.OnNetworkSpawn();

            if (TurnManager.Instance) TurnManager.Instance.SyncTurn(CurrentTurn.Value);
            CurrentTurn.OnValueChanged += (oldV, NewV) =>
            {
                if (TurnManager.Instance) TurnManager.Instance.SyncTurn(NewV);
            };

            // Hook move number changes for local UI listeners (purely cosmetic on clients)
            MoveNumber.OnValueChanged += (oldV, newV) =>
            {
                TurnCounterUI.BroadcastMoveNumber(newV);
            };

            if (IsServer) StartCoroutine(BoardHeartbeat());
        }
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
        Vector2Int requested = new Vector2Int(spawnX, spawnY);

        // Decide the final spawn on the SERVER
        Vector2Int spawn = ChessBoard.Instance.GetPieceAt(requested) == null
            ? requested
            : ChessBoard.Instance.FindNearestAvailableSquare(requested);

        if (!ChessBoard.Instance.IsInsideBoard(spawn)) return;

        // (Optional tidy) This second nearest-check is redundant because 'spawn'
        // is either empty or returned by FindNearestAvailableSquare already.
        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);
        if (spawn.x == -1) return; // no space; abort

        var prefab = BoardInitializer.Instance.GetPrefab(team, pieceType);
        if (prefab == null) { Debug.LogWarning($"No prefab for {team} {pieceType}"); return; }

        var go = Instantiate(prefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        var newPiece = go.GetComponent<ChessPiece>();
        newPiece.team = team;
        newPiece.pieceType = pieceType;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        newPiece.MarkAsResurrected();

        // Allocate a shared Id and register BEFORE placing
        newPiece.Id = ChessBoard.Instance.AllocatePieceId();
        ChessBoard.Instance.RegisterPiece(newPiece);
        ChessBoard.Instance.PlacePiece(newPiece, spawn);

        // Update server graveyard + notify clients to update UI
        ChessBoard.Instance.RemoveCapturedPieceByTypeAndTeam(pieceType, team);
        RemoveFromGraveyardClientRpc(team, pieceType);

        // Send the ACTUAL server-chosen cell to clients
        ResurrectClientRpc(team, pieceType, spawn.x, spawn.y, newPiece.Id);
    }

    [ClientRpc]
    void ResurrectClientRpc(TeamColor team, PieceType pieceType, int spawnX, int spawnY, int newId)
    {
    // Host already has the server-instantiated piece — do NOT also instantiate on the host.
        if (Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsHost)
                   {
                        // (Optional) ensure local caches/index/UI are up-to-date, but skip instantiation
            ChessBoard.Instance.RebuildIndexFromScene();
                        return;
                    }
        var spawn = new Vector2Int(spawnX, spawnY);
        var prefab = BoardInitializer.Instance.GetPrefab(team, pieceType);
        if (prefab == null) return;

        var go = Instantiate(prefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        var p = go.GetComponent<ChessPiece>();
        p.team = team;
        p.pieceType = pieceType;
        p.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        p.MarkAsResurrected();

        // Use the server-assigned Id and register locally
        p.Id = newId;
        ChessBoard.Instance.RegisterPiece(p);
        ChessBoard.Instance.PlacePiece(p, spawn);
    }
    [ClientRpc]
    void RemoveFromGraveyardClientRpc(TeamColor team, PieceType type)
    {
        ChessBoard.Instance.RemoveCapturedPieceByTypeAndTeam(type, team);
    }

    [ClientRpc]
    void MoveRookClientRpc(int rookId, int toX, int toY)
    {
        var rook = ChessBoard.Instance.GetPieceById(rookId);
        if (rook == null)
        {
            ChessBoard.Instance.RebuildIndexFromScene();
            rook = ChessBoard.Instance.GetPieceById(rookId);
            if (rook == null) return;
        }

        ChessBoard.Instance.MovePieceLocal(rook, new Vector2Int(toX, toY));
    }
    
    [ClientRpc]
    public void ShowVictoryClientRpc(string winnerText)
    {
        var board = ChessBoard.Instance;
        if (board == null) return;

        board.gameOver = true; // optional local freeze
        if (board.victoryScreen) board.victoryScreen.SetActive(true);
        if (board.victoryText) board.victoryText.text = winnerText;
        if (board.audioSource && board.victoryClip)
            board.audioSource.PlayOneShot(board.victoryClip);
    }

}
