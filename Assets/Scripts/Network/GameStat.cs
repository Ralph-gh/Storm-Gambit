using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }
    // =========================================================
    // NETWORK PROMOTION
    // =========================================================

    private int pendingPromotionPawnId = -1;
    private ulong pendingPromotionClientId;
    // Which side can act
    public NetworkVariable<TeamColor> CurrentTurn = new NetworkVariable<TeamColor>(TeamColor.White);

    // Quick piece addressing: ChessPiece must have a stable unique Id.
    // If you don't have it yet, add `public int Id;` to ChessPiece and assign in BoardInitializer.
    // We'll use RPCs to move pieces by Id to avoid full-state replication for now.
    
    public NetworkVariable<int> MoveNumber = new NetworkVariable<int>(0);//used for turn counter in network play
    void Awake() => Instance = this;

    private void ShowSpellNotification(string message)
    {
        if (SpellOverlayManager.Instance == null)
        {
            Debug.LogWarning(
                "[GameState] SpellOverlayManager not found."
            );
            return;
        }

        SpellOverlayManager.Instance.ShowNotificationPopup(
            message
        );
    }
    public bool IsMyTurn(TeamColor mySide) => CurrentTurn.Value == mySide;

    [ServerRpc(RequireOwnership = false)]

    public void RequestMoveServerRpc(int pieceId, int targetX, int targetY, ServerRpcParams p = default)
    {
        // Always ensure the server-side board/index are fresh before any legality checks
        ChessBoard.Instance.RebuildBoardAndIndexFromScene();
        var sender = p.Receive.SenderClientId;
        // Do not allow another chess move while a promotion choice is pending.
        if (pendingPromotionPawnId >= 0)
        {
            Debug.Log("[PROMOTION] Move rejected: waiting for promotion choice.");
            return;
        }
        var player = NetPlayer.FindByClient(sender);
        if (player == null || player.Side.Value != CurrentTurn.Value)
        {
            Debug.Log($"[SRPC] not your turn | sender={sender} side={player?.Side.Value} turn={CurrentTurn.Value}");
            return;
        }

        var piece = ChessBoard.Instance.GetPieceById(pieceId);

        if (piece == null)
        {
            Debug.Log($"[SRPC] no piece id={pieceId}");
            return;
        }

        // ==========================================
        // STUN and freeze SERVER AUTHORITY
        // ==========================================
        if (piece.IsStunned)
        {
            Debug.Log(
                $"[SRPC] move rejected: {piece.pieceType}#{piece.Id} is stunned."
            );

            return;
        }
        if (piece.IsFrozen)
        {
            Debug.Log(
                $"[SRPC] move rejected: {piece.pieceType}#{piece.Id} is frozen."
            );

            return;
        }
        if (piece.IsDivinelyProtected)
        {
            Debug.Log(
                $"[SRPC] move rejected: {piece.pieceType}#{piece.Id} is divinely protected."
            );

            return;
        }

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
            {
                // Server (host) draws for itself locally
                DrawSpellForBothPlayersLocal();

                // Then notify remote clients to draw
                DrawSpellForBothPlayersClientRpc();
            }
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
            if (epVictim.IsDivinelyProtected ||epVictim.IsFrozen)
            {
                Debug.Log(
                    "[SRPC] EP blocked: victim protected/frozen"
                );

                return;
            }

            capturedId = epVictim.Id;
            Debug.Log($"[SRPC] EN PASSANT CAPTURE Pawn#{capturedId} at {victimCell}");
            ChessBoard.Instance.CapturePiece(victimCell); // note: capture the pawn at its square
        }
        else if (victim != null)
        {
            if (victim.IsDivinelyProtected || victim.IsFrozen)
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

        bool explosiveTrapTriggered = ChessBoard.Instance.TryConsumeEnemyExplosiveTrap(
            to,
            piece.team,
            out TeamColor trapOwner
        );

        if (explosiveTrapTriggered)
        {
            // Clear the mover from the server board and record it in the graveyard,
            // but keep its GameObject alive until the RPC removes it everywhere.
            ChessBoard.Instance.PrepareExplosiveTrapCaptureServer(piece);

            ApplyExplosiveTrapMoveClientRpc(piece.Id,from.x,from.y,to.x,to.y,capturedId,trapOwner);

            StartCoroutine(CleanupExplodedMoverServerAfterVFX(piece));
        }
        else
        {
            ApplyMoveClientRpc(piece.Id, from.x, from.y,to.x,to.y, capturedId);
        }
        // Move king on server
        ChessBoard.Instance.ExecuteMoveServer(piece, to);
        // =========================================================
        // NETWORK PROMOTION
        // =========================================================

        bool needsPromotion =
            !explosiveTrapTriggered &&
            piece.pieceType == PieceType.Pawn &&
            Pawn.ShouldPromote(to, piece.team);

        if (needsPromotion)
        {
            Debug.Log(
                $"[PROMOTION/SERVER] Pawn#{piece.Id} reached {to}. " +
                $"Waiting for {piece.team} promotion choice."
            );

            pendingPromotionPawnId = piece.Id;
            pendingPromotionClientId = sender;

            // Promotion ends any en-passant window.
            ChessBoard.Instance.ClearEnPassant();

            SetEnPassantClientRpc(
                -1,
                -1,
                -1
            );

            // ONLY the player who owns this pawn should see the panel.
            ClientRpcParams targetPlayer =
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds =
                            new ulong[] { sender }
                    }
                };

            ShowPromotionClientRpc(
                piece.Id,
                to.x,
                to.y,
                piece.team,
                targetPlayer
            );

            // VERY IMPORTANT:
            // Do NOT change the turn yet.
            return;
        }
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
        //ApplyMoveClientRpc(piece.Id, to.x, to.y, capturedId); Normal move applied above no need to repeat, line code left, to be deleted if game functions
        if (rookId >= 0)
            MoveRookClientRpc(rookId, rookToX, rookToY);
        // Send the EP window for the NEXT move
        SetEnPassantClientRpc(epX, epY, epPawnId);
        // flip turn...
        //var next = (CurrentTurn.Value == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        CurrentTurn.Value = next;
    }

    [ClientRpc]
    void ApplyExplosiveTrapMoveClientRpc(
    int moverId,
    int fromX,
    int fromY,
    int toX,
    int toY,
    int capturedId,
    TeamColor trapOwner)
    {
        Vector2Int to = new Vector2Int(toX, toY);
        Vector2Int from = new Vector2Int(fromX, fromY);
        string role = Unity.Netcode.NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT";
        Debug.Log($"[TRAP/{role}] mover={moverId} exploded at {to}, owner={trapOwner}");

        // If the move captured a piece sitting above the trap, remove that victim first.
        if (capturedId >= 0)
        {
            var victim = ChessBoard.Instance.GetPieceById(capturedId);
            if (victim != null)
            {
                ChessBoard.Instance.AddCapturedPiece(victim);
                ChessBoard.Instance.RemovePieceLocal(victim);
            }
        }

        var mover = ChessBoard.Instance.GetPieceById(moverId);
        if (mover == null)
        {
            ChessBoard.Instance.RebuildIndexFromScene();
            mover = ChessBoard.Instance.GetPieceById(moverId);
        }
        // IMPORTANT:
        // Server accepted the move.
        // Unlock this piece.
        mover.ConfirmNetworkMove();

        if (mover != null)
        {
            // Remote clients still have the mover on its origin square. The host already
            // applied the server move, so only its transform needs to be centered.
            if (mover.currentCell != to)
                ChessBoard.Instance.MovePieceLocal(mover, to);
            else if (BoardInitializer.Instance != null)
                mover.transform.position = BoardInitializer.Instance.GetWorldPosition(to);

            // The server/host graveyard was updated before this RPC. Remote clients
            // need their own local graveyard record for Resurrection UI consistency.
            if (!IsServer)
                ChessBoard.Instance.AddCapturedPiece(mover);

            StartCoroutine(PlayExplosiveTrapSequenceClient(mover, to));
        }
        else
        {
            Debug.LogWarning($"[TRAP/{role}] exploded mover {moverId} was not found.");
            ChessBoard.Instance.HideExplosiveTrapMarker(to);
            ChessBoard.Instance.PlayExplosiveTrapEffect(to);
        }
        // Show the accepted move even though the piece exploded.
        MoveIndicator.Instance?.ShowMove(from, to);
        TurnManager.Instance?.SyncTurn(CurrentTurn.Value);
    }

    private System.Collections.IEnumerator CleanupExplodedMoverServerAfterVFX(
    ChessPiece piece)
    {
        // Keep the real chess piece alive while clients display
        // explosion + shatter.
        yield return new WaitForSeconds(0.55f);

        if (piece != null)
        {
            ChessBoard.Instance.UnregisterPiece(piece);
            Destroy(piece.gameObject);
        }
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
        // Only remote clients run this (server/host already did it locally)
        if (IsServer) return;
        DrawSpellForBothPlayersLocal();
    }

    private void DrawSpellForBothPlayersLocal()
    {
        var drawers = FindObjectsByType<CardDrawer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var d in drawers)
            d.DrawOneSpellCard();
    }
    private System.Collections.IEnumerator PlayExplosiveTrapSequenceClient(
    ChessPiece mover,
    Vector2Int trapCell)
    {
        if (mover == null)
            yield break;

        // --------------------------------
        // 1. Piece is already on trap cell
        // --------------------------------

        if (BoardInitializer.Instance != null)
        {
            mover.transform.position =
                BoardInitializer.Instance.GetWorldPosition(trapCell);
        }

        // --------------------------------
        // 2. Remove marker
        // --------------------------------

        ChessBoard.Instance.HideExplosiveTrapMarker(trapCell);

        // --------------------------------
        // 3. Explosion
        // --------------------------------

        ChessBoard.Instance.PlayExplosiveTrapEffect(trapCell);

        // Give explosion flash a moment to register.
        yield return new WaitForSeconds(0.05f);

        // --------------------------------
        // 4. Shatter
        // --------------------------------

        if (PieceShatterVFX.Instance != null)
        {
            PieceShatterVFX.Instance.Play(mover);
        }
        else
        {
            Debug.LogWarning(
                "[TRAP/NET] PieceShatterVFX missing on this client."
            );

            SpriteRenderer sr =
                mover.GetComponent<SpriteRenderer>();

            if (sr != null)
                sr.enabled = false;
        }

        // --------------------------------
        // 5. Let fragments fly
        // --------------------------------

        yield return new WaitForSeconds(0.45f);

        // --------------------------------
        // 6. Remote visual cleanup
        // --------------------------------

        // Server/host object is cleaned by
        // CleanupExplodedMoverServerAfterVFX().
        if (!IsServer && mover != null)
        {
            ChessBoard.Instance.RemovePieceLocal(mover);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyFreezeServerRpc(int pieceId, ServerRpcParams p = default)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null) return;

        ApplyFreezeClientRpc(pieceId);
    }

    [ClientRpc]
    void ApplyFreezeClientRpc(int pieceId)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);
        if (piece == null)
        {
            ChessBoard.Instance.RebuildIndexFromScene();
            piece = ChessBoard.Instance.GetPieceById(pieceId);
        }

        if (piece != null)
            piece.ApplyFreeze(2);

        TeamColor activeSide = CurrentTurn.Value;

        if (!TurnManager.Instance.HasAnyMovablePiece(activeSide))
        {
            Debug.Log(
                $"[SERVER] {activeSide} has no movable pieces after Freeze."
            );

            // advance using your normal authoritative server turn method
        }
    }
    [ClientRpc]
    void ApplyMoveClientRpc( int moverId, int fromX,int fromY,int toX,int toY,int capturedId)
    {
        string role =
            NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT";

        Vector2Int from = new Vector2Int(fromX, fromY);
        Vector2Int to = new Vector2Int(toX, toY);

        Debug.Log(
            $"[RPC/{role}] mover={moverId} from={from} to={to} captured={capturedId}"
        );

        var mover = ChessBoard.Instance.GetPieceById(moverId);

        if (mover == null)
        {
            Debug.LogWarning(
                $"[RPC/{role}] mover {moverId} missing - rebuilding index"
            );

            ChessBoard.Instance.RebuildIndexFromScene();

            mover = ChessBoard.Instance.GetPieceById(moverId);

            if (mover == null)
            {
                Debug.LogWarning(
                    $"[RPC/{role}] mover {moverId} STILL missing"
                );

                return;
            }
        }

        // =========================================================
        // CAPTURE
        // =========================================================

        if (capturedId >= 0)
        {
            var victim =
                ChessBoard.Instance.GetPieceById(capturedId);

            if (victim != null)
            {
                Debug.Log(
                    $"[RPC/{role}] removing victim id={capturedId} at {victim.currentCell}"
                );

                ChessBoard.Instance.AddCapturedPiece(victim);
                ChessBoard.Instance.RemovePieceLocal(victim);
            }
            else
            {
                Debug.LogWarning(
                    $"[RPC/{role}] victim {capturedId} not found (already removed?)"
                );
            }
        }
        if (capturedId >= 0)
        {
            if (ChessBoard.Instance.audioSource != null &&
                ChessBoard.Instance.captureClip != null)
            {
                ChessBoard.Instance.audioSource.PlayOneShot(
                    ChessBoard.Instance.captureClip
                );
            }
        }
        else
        {
            if (mover.audioSource != null &&
                mover.moveClip != null)
            {
                mover.audioSource.PlayOneShot(
                    mover.moveClip
                );
            }
        }
        // =========================================================
        // APPLY VISUAL / LOCAL BOARD MOVE
        // =========================================================

        // Prevent accidental same-square MovePieceLocal(to -> to)
        if (mover.currentCell != to)
        {
            ChessBoard.Instance.MovePieceLocal(
                mover,
                to
            );
        }
        else if (BoardInitializer.Instance != null)
        {
            mover.transform.position =
                BoardInitializer.Instance.GetWorldPosition(to);
        }

        // =========================================================
        // MOVE INDICATOR
        // =========================================================

        MoveIndicator.Instance?.ShowMove(
            from,
            to
        );

        TurnManager.Instance?.SyncTurn(
            CurrentTurn.Value
        );
    }
    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams p = default)
    {
        var senderClientId = p.Receive.SenderClientId;
        var netPlayer = NetPlayer.FindByClient(senderClientId);

        if (netPlayer == null)
            return;

        if (netPlayer.Side.Value != CurrentTurn.Value)
            return;

        CurrentTurn.Value =
            CurrentTurn.Value == TeamColor.White
            ? TeamColor.Black
            : TeamColor.White;

        ChessBoard.Instance.ClearEnPassant();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

#if UNITY_SERVER || UNITY_EDITOR
        // Reset castling state on server/editor start.
        foreach (var p in FindObjectsByType<ChessPiece>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
        {
            if (p.pieceType == PieceType.King ||
                p.pieceType == PieceType.Rook)
            {
                p.hasMoved = false;
            }
        }

        // Make sure server board/index matches the scene.
        if (ChessBoard.Instance != null)
        {
            ChessBoard.Instance.RebuildBoardAndIndexFromScene();
        }
#endif

        // Initial local turn sync.
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.SyncTurn(CurrentTurn.Value);
        }

        // Keep local turn state synced whenever server changes turn.
        CurrentTurn.OnValueChanged += HandleCurrentTurnChanged;

        // Keep move counter UI synced.
        MoveNumber.OnValueChanged += HandleMoveNumberChanged;

        // Only the server should run the board heartbeat.
        if (IsServer)
        {
            StartCoroutine(BoardHeartbeat());
        }
    }
    private void HandleCurrentTurnChanged(
       TeamColor oldValue,
       TeamColor newValue)
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.SyncTurn(newValue);
        }

        // SAFETY:
        // Once the authoritative server changes turn,
        // no piece should still be waiting for move confirmation.
        ChessPiece[] pieces = FindObjectsByType<ChessPiece>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (ChessPiece piece in pieces)
        {
            piece.ConfirmNetworkMove();
        }
    }

    private void HandleMoveNumberChanged(
        int oldValue,
        int newValue)
    {
        TurnCounterUI.BroadcastMoveNumber(newValue);
    }
    public override void OnNetworkDespawn()
    {
        CurrentTurn.OnValueChanged -= HandleCurrentTurnChanged;
        MoveNumber.OnValueChanged -= HandleMoveNumberChanged;

        base.OnNetworkDespawn();
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
    public void PlaceExplosiveTrapServerRpc(
         int x,
         int y,
         ServerRpcParams p = default)
    {
        var player = NetPlayer.FindByClient(p.Receive.SenderClientId);
        if (player == null) return;
        if (player.Side.Value != CurrentTurn.Value) return;

        Vector2Int cell = new Vector2Int(x, y);
        TeamColor owner = player.Side.Value;

        if (!ChessBoard.Instance.TryPlaceExplosiveTrap(cell, owner))
        {
            Debug.Log($"[TRAP/SRPC] Rejected placement for {owner} at {cell}");
            return;
        }

        PlaceExplosiveTrapClientRpc(owner, x, y);
    }

    [ClientRpc]
    void PlaceExplosiveTrapClientRpc(TeamColor owner, int x, int y)
    {
        Vector2Int cell = new Vector2Int(x, y);

        // The trap is hidden from the opponent. Only its owner receives a marker.
        if (NetPlayer.Local != null && NetPlayer.Local.Side.Value == owner)
            ChessBoard.Instance.ShowExplosiveTrapMarker(cell);
        // BOTH players know that a trap was planted.
        ShowSpellNotification(
            "An explosive trap has been planted!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TeleportPieceServerRpc(
    int pieceId,
    int x,
    int y,
    ServerRpcParams p = default)
    {
        var piece = ChessBoard.Instance.GetPieceById(pieceId);

        if (piece == null)
        {
            Debug.Log($"[SRPC] Teleport: no piece {pieceId}");
            return;
        }

        Vector2Int to = new Vector2Int(x, y);

        if (!ChessBoard.Instance.IsInsideBoard(to))
            return;

        // Teleport destination must still be empty.
        if (ChessBoard.Instance.GetPieceAt(to) != null)
            return;

        Vector2Int from = piece.currentCell;

        // =========================================================
        // CHECK EXPLOSIVE TRAP BEFORE NORMAL TELEPORT RESOLUTION
        // =========================================================

        bool explosiveTrapTriggered =
            ChessBoard.Instance.TryConsumeEnemyExplosiveTrap(
                to,
                piece.team,
                out TeamColor trapOwner
            );

        if (explosiveTrapTriggered)
        {
            Debug.Log(
                $"[TELEPORT/TRAP] {piece.pieceType}#{piece.Id} " +
                $"teleported from {from} onto enemy trap at {to}"
            );

            // -----------------------------------------------------
            // Move the piece onto the trap square on the SERVER.
            // -----------------------------------------------------

            ChessBoard.Instance.MovePiece(
                piece.currentCell,
                to
            );

            piece.SetPosition(
                to,
                BoardInitializer.Instance.GetWorldPosition(to)
            );

            piece.hasMoved = true;

            // -----------------------------------------------------
            // Record it as captured without destroying immediately.
            // Existing trap animation RPC needs the object alive.
            // -----------------------------------------------------

            ChessBoard.Instance.PrepareExplosiveTrapCaptureServer(
                piece
            );

            // Teleport lands on an EMPTY square,
            // therefore there is no captured victim.
            int capturedId = -1;

            ApplyExplosiveTrapMoveClientRpc(
                piece.Id,
                from.x,
                from.y,
                to.x,
                to.y,
                capturedId,
                trapOwner
            );

            StartCoroutine(
                CleanupExplodedMoverServerAfterVFX(piece)
            );

            return;
        }

        // =========================================================
        // NORMAL TELEPORT
        // =========================================================

        ChessBoard.Instance.MovePiece(
            piece.currentCell,
            to
        );

        piece.SetPosition(
            to,
            BoardInitializer.Instance.GetWorldPosition(to)
        );

        piece.hasMoved = true;

        TeleportPieceClientRpc(
            pieceId,
            x,
            y
        );
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
        piece.ApplyStunOneTurn();//stun piece for one turn to avoid being overpowered
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
        BoardFlipController.Instance?.ApplyOrientation(newPiece);
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
    void ResurrectClientRpc(
    TeamColor team,
    PieceType pieceType,
    int spawnX,
    int spawnY,
    int newId)
    {
        // Host already created its copy on the server.
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsHost)
        {
            ChessBoard.Instance.RebuildIndexFromScene();
            return;
        }

        var spawn = new Vector2Int(spawnX, spawnY);

        var prefab = BoardInitializer.Instance.GetPrefab(team, pieceType);
        if (prefab == null) return;

        var go = Instantiate(
            prefab,
            BoardInitializer.Instance.GetWorldPosition(spawn),
            Quaternion.identity
        );

        var p = go.GetComponent<ChessPiece>();

        p.team = team;
        p.pieceType = pieceType;

        p.SetPosition(
            spawn,
            BoardInitializer.Instance.GetWorldPosition(spawn)
        );

        p.MarkAsResurrected();

        // FIX:
        // Newly spawned client-side pieces must respect this client's
        // current board orientation.
        BoardFlipController.Instance?.ApplyOrientation(p);

        // Use the server-assigned ID
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
    [ClientRpc]
    private void ShowPromotionClientRpc(
    int pawnId,
    int x,
    int y,
    TeamColor team,
    ClientRpcParams rpcParams = default)
    {
        ChessPiece pawn =
            ChessBoard.Instance.GetPieceById(pawnId);

        if (pawn == null)
        {
            ChessBoard.Instance.RebuildIndexFromScene();

            pawn =
                ChessBoard.Instance.GetPieceById(pawnId);
        }

        if (pawn == null)
        {
            Debug.LogWarning(
                $"[PROMOTION/CLIENT] Pawn {pawnId} not found."
            );

            return;
        }

        Vector2Int cell =
            new Vector2Int(x, y);

        ChessBoard.Instance.pawnToPromote = pawn;

        Vector3 worldPosition =
            BoardInitializer.Instance.GetWorldPosition(cell);

        if (ChessBoard.Instance.promotionSelector != null)
        {
            ChessBoard.Instance.promotionSelector.Show(
                worldPosition,
                cell,
                team
            );
        }
        else
        {
            Debug.LogWarning(
                "[PROMOTION/CLIENT] PromotionSelector is missing."
            );
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void RequestPromotionServerRpc(
    int pawnId,
    PieceType promoteTo,
    ServerRpcParams p = default)
    {
        ulong sender =
            p.Receive.SenderClientId;

        // =========================================================
        // VALIDATE REQUEST
        // =========================================================

        if (pendingPromotionPawnId < 0)
        {
            Debug.LogWarning(
                "[PROMOTION/SERVER] No promotion pending."
            );

            return;
        }

        if (sender != pendingPromotionClientId)
        {
            Debug.LogWarning(
                "[PROMOTION/SERVER] Wrong client attempted promotion."
            );

            return;
        }

        if (pawnId != pendingPromotionPawnId)
        {
            Debug.LogWarning(
                "[PROMOTION/SERVER] Wrong pawn ID."
            );

            return;
        }

        if (promoteTo != PieceType.Queen &&
            promoteTo != PieceType.Rook &&
            promoteTo != PieceType.Bishop &&
            promoteTo != PieceType.Knight)
        {
            Debug.LogWarning(
                "[PROMOTION/SERVER] Invalid promotion piece."
            );

            return;
        }

        ChessPiece pawn =
            ChessBoard.Instance.GetPieceById(pawnId);

        if (pawn == null)
        {
            Debug.LogWarning(
                $"[PROMOTION/SERVER] Pawn {pawnId} not found."
            );

            return;
        }

        if (pawn.pieceType != PieceType.Pawn)
            return;

        if (!Pawn.ShouldPromote(
            pawn.currentCell,
            pawn.team))
        {
            Debug.LogWarning(
                "[PROMOTION/SERVER] Pawn is not on promotion rank."
            );

            return;
        }

        Vector2Int cell =
            pawn.currentCell;

        TeamColor team =
            pawn.team;

        GameObject prefab =
            BoardInitializer.Instance.GetPrefab(
                team,
                promoteTo
            );

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[PROMOTION/SERVER] Missing prefab for {team} {promoteTo}."
            );

            return;
        }

        int oldPawnId =
            pawn.Id;

        // =========================================================
        // REMOVE PAWN ON SERVER
        // =========================================================

        pawn.gameObject.SetActive(false);

        ChessBoard.Instance.RemovePieceLocal(
            pawn
        );

        // =========================================================
        // CREATE PROMOTED PIECE ON SERVER
        // =========================================================

        Vector3 worldPosition =
            BoardInitializer.Instance.GetWorldPosition(cell);

        GameObject go =
            Instantiate(
                prefab,
                worldPosition,
                Quaternion.identity
            );

        ChessPiece newPiece =
            go.GetComponent<ChessPiece>();

        newPiece.team = team;
        newPiece.pieceType = promoteTo;

        newPiece.SetPosition(
            cell,
            worldPosition
        );

        newPiece.hasMoved = true;
        newPiece.startingCell = cell;
        newPiece.originalPrefab = prefab;

        SpriteRenderer sr =
            go.GetComponent<SpriteRenderer>();

        if (sr != null)
            newPiece.pieceSprite = sr.sprite;

        newPiece.Id =
            ChessBoard.Instance.AllocatePieceId();

        ChessBoard.Instance.RegisterPiece(
            newPiece
        );

        ChessBoard.Instance.PlacePiece(
            newPiece,
            cell
        );

        BoardFlipController.Instance
            ?.ApplyOrientation(newPiece);

        // =========================================================
        // SYNC CLIENTS
        // =========================================================

        PromotionResolvedClientRpc(
            oldPawnId,
            newPiece.Id,
            team,
            promoteTo,
            cell.x,
            cell.y
        );

        // Promotion is now finished.
        pendingPromotionPawnId = -1;

        // NOW the turn can change.
        CurrentTurn.Value =
            team == TeamColor.White
                ? TeamColor.Black
                : TeamColor.White;
    }
    [ClientRpc]
    private void PromotionResolvedClientRpc(
    int oldPawnId,
    int newPieceId,
    TeamColor team,
    PieceType newType,
    int x,
    int y)
    {
        // Clear local promotion state.
        ChessBoard.Instance.pawnToPromote = null;

        if (ChessBoard.Instance.promotionSelector != null)
        {
            ChessBoard.Instance
                .promotionSelector
                .gameObject
                .SetActive(false);
        }

        // Host already performed the replacement.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsHost)
        {
            ChessBoard.Instance
                .RebuildIndexFromScene();

            return;
        }

        // =========================================================
        // REMOVE OLD PAWN
        // =========================================================

        ChessPiece oldPawn =
            ChessBoard.Instance.GetPieceById(
                oldPawnId
            );

        if (oldPawn != null)
        {
            oldPawn.gameObject.SetActive(false);

            ChessBoard.Instance.RemovePieceLocal(
                oldPawn
            );
        }

        // =========================================================
        // CREATE PROMOTED PIECE
        // =========================================================

        Vector2Int cell =
            new Vector2Int(x, y);

        GameObject prefab =
            BoardInitializer.Instance.GetPrefab(
                team,
                newType
            );

        if (prefab == null)
            return;

        Vector3 worldPosition =
            BoardInitializer.Instance.GetWorldPosition(
                cell
            );

        GameObject go =
            Instantiate(
                prefab,
                worldPosition,
                Quaternion.identity
            );

        ChessPiece newPiece =
            go.GetComponent<ChessPiece>();

        newPiece.team = team;
        newPiece.pieceType = newType;

        newPiece.SetPosition(
            cell,
            worldPosition
        );

        newPiece.hasMoved = true;
        newPiece.startingCell = cell;
        newPiece.originalPrefab = prefab;

        SpriteRenderer sr =
            go.GetComponent<SpriteRenderer>();

        if (sr != null)
            newPiece.pieceSprite = sr.sprite;

        newPiece.Id =
            newPieceId;

        ChessBoard.Instance.RegisterPiece(
            newPiece
        );

        ChessBoard.Instance.PlacePiece(
            newPiece,
            cell
        );

        // Important if this client currently has the board flipped.
        BoardFlipController.Instance
            ?.ApplyOrientation(newPiece);
    }
}
