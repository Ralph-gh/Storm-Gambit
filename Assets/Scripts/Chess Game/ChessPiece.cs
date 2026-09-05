using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum TeamColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King}
public class ChessPiece : NetworkBehaviour
{
    public int Id { get; set; }
    private bool _divineOppTurnSeen = false; 
    public TeamColor team;
    public PieceType pieceType;
    public bool hasMoved= false;
    public Vector2Int currentCell;
    public AudioClip moveClip;
    public AudioSource audioSource;
    private bool canDrag = true; 
    public Vector2Int initialCell { get; private set; } // Added tracked initial cell
    public bool hasBeenInitialized = false;

    public Vector2Int startingCell; //used to store the starting position of a piece for later use in spells
    public GameObject originalPrefab; //Hard reset on resurrection 

    public Vector2Int GetStartingCell() => startingCell;
    public Sprite pieceSprite;

    private bool divinelyProtected = false; // Divine protection state
    public bool IsDivinelyProtected => divinelyProtected;
    private TeamColor protectionOwnerTeam; //Divine protection Owner
    private System.Action<TeamColor> _turnListener;//Used for turn logic
    //Apply protection for exactly one opponent turn
    //for freezing spell and freeze mage ability
    private bool isFrozen = false;
    public bool IsFrozen => isFrozen;

    private int frozenOwnerTurnsRemaining = 0;
    private System.Action<TeamColor> _freezeTurnListener;

    [SerializeField] private Color frozenColor = new Color(0.65f, 0.85f, 1f, 1f);
    private Color _normalBaseColor;
    //freezing end
    [Header("Stun Status")]
    [SerializeField] private bool isStunned;
    public bool IsStunned => isStunned;
    [SerializeField] private GameObject stunnedMarkerPrefab;
    //-- stun status end --
    private GameObject _stunnedMarker;
    private System.Action<TeamColor> _stunTurnListener;
    private bool isDragging = false;
    private Vector3 originalPosition;
    private Vector3 offset;

    // For visual purpose only
    public GameObject divineSpherePrefab;   // assign the sphere prefab in the Inspector
    private GameObject _divineSphere;       // runtime instance

    //Hover Highlights
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.6f, 1f, 0.85f); // soft icy tint
    private SpriteRenderer _sr;
    private Color _baseColor;
    private bool isResurrected = false;
    //freeze visual
    [Header("Freeze Visual")]
    public GameObject frozenSquareMarkerPrefab;
    private GameObject _frozenSquareMarker;

    //Network
    public NetworkVariable<int> PieceId = new(0);
    public NetworkVariable<TeamColor> Team = new(TeamColor.White,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Vector2Int> BoardCell = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
        {
            _baseColor = _sr.color;
            _normalBaseColor = _sr.color;
        }
    }
    public void SetPosition(Vector2Int cellPosition, Vector3 worldPosition)
    {
        currentCell = cellPosition;
        transform.position = worldPosition;
        hasMoved = false; // Reset in case piece is re-used or repositioned

        if (!hasBeenInitialized)
        {
            initialCell = cellPosition;
            hasBeenInitialized = true;
        }
    }
    public void ApplyStunOneTurn()
    {
        // Refresh existing stun if somehow applied again.
        RemoveStun();

        isStunned = true;

        ShowStunnedMarker();

        Debug.Log($"{name} is stunned.");

        if (TurnManager.Instance == null)
            return;

        bool opponentTurnSeen = false;

        _stunTurnListener = (TeamColor activeTeam) =>
        {
            // Teleport is cast during this piece's own turn.
            // Wait until the opponent gets their turn.
            if (!opponentTurnSeen && activeTeam != team)
            {
                opponentTurnSeen = true;
                return;
            }

            // Once play returns to this piece's owner,
            // the stun expires.
            if (opponentTurnSeen && activeTeam == team)
            {
                RemoveStun();
            }
        };

        TurnManager.Instance.OnTurnChanged += _stunTurnListener;
    }

    public void RemoveStun()
    {
        if (!isStunned)
            return;

        isStunned = false;

        if (_stunnedMarker != null)
        {
            Destroy(_stunnedMarker);
            _stunnedMarker = null;
        }

        if (_stunTurnListener != null &&
            TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged -= _stunTurnListener;
            _stunTurnListener = null;
        }

        Debug.Log($"{name} is no longer stunned.");
    }

    private void ShowStunnedMarker()
    {
        if (stunnedMarkerPrefab == null || _stunnedMarker != null)
            return;

        Vector3 worldPos = BoardInitializer.Instance != null
            ? BoardInitializer.Instance.GetWorldPosition(currentCell)
            : transform.position;

        _stunnedMarker = Instantiate(
            stunnedMarkerPrefab,
            worldPos,
            Quaternion.identity
        );

        // Keep it attached to the piece.
        _stunnedMarker.transform.SetParent(
            transform,
            worldPositionStays: true
        );

        // Keep it centered on the square.
        _stunnedMarker.transform.position = worldPos;

        // IMPORTANT:
        // Do not override sorting layer/order here.
        // Use the prefab's SpriteRenderer settings.
    }

    public void ApplyDivineProtectionOneTurn()
    {
        if (divinelyProtected) return;

        divinelyProtected = true;
        protectionOwnerTeam = team;

        // Spawn + parent
        if (divineSpherePrefab != null && _divineSphere == null)
        {
            _divineSphere = Instantiate(divineSpherePrefab, transform.position, Quaternion.identity);
            _divineSphere.transform.SetParent(transform, worldPositionStays: false); // inherit scale
            _divineSphere.transform.localPosition = Vector3.zero;

            // Make sure it renders above the piece
            var pieceSR = GetComponent<SpriteRenderer>();
            var sphereSR = _divineSphere.GetComponent<SpriteRenderer>();
            if (pieceSR && sphereSR)
            {
                sphereSR.sortingLayerID = pieceSR.sortingLayerID;
                sphereSR.sortingOrder = pieceSR.sortingOrder + 1;
            }
        }

        // Wait for: owner -> opponent -> owner, then remove
        _divineOppTurnSeen = false; // reset every time we apply
        _turnListener = (TeamColor activeTeam) =>
        {
            // First time we see the opponent's turn, arm the removal
            if (!_divineOppTurnSeen && activeTeam != protectionOwnerTeam)
            {
                _divineOppTurnSeen = true;
                return;
            }

            // After we've seen opponent, remove when it returns to owner
            if (_divineOppTurnSeen && activeTeam == protectionOwnerTeam)
            {
                RemoveDivineProtection();
                TurnManager.Instance.OnTurnChanged -= _turnListener;
                _turnListener = null;
            }
        };
        TurnManager.Instance.OnTurnChanged += _turnListener;
    }

    public void RemoveDivineProtection()
    {
        if (!divinelyProtected) return;
        divinelyProtected = false;

        // Clean up the sphere if present
        if (_divineSphere != null)
        {
            Destroy(_divineSphere);
            _divineSphere = null;
        }
        // Optional: remove glow/icon here
        // e.g. GetComponent<SpriteRenderer>().color = Color.white;
    }

    public void ApplyFreeze(int ownerTurnsToSkip = 2)
    {
        if (isFrozen) return;

        isFrozen = true;
        frozenOwnerTurnsRemaining = ownerTurnsToSkip;

        if (isStunned)
        {
            Debug.Log($"{name} is stunned and cannot move.");
            return;
        }

        if (_sr != null)
            _sr.color = frozenColor;
        ShowFrozenMarker();
        TeamColor previousTurn = TurnManager.Instance.currentTurn;
        _freezeTurnListener = (TeamColor activeTeam) =>
        {
            // Count down when the frozen piece owner's turn ENDS
            if (previousTurn == team && activeTeam != team)
            {
                frozenOwnerTurnsRemaining--;

                if (frozenOwnerTurnsRemaining <= 0)
                {
                    RemoveFreeze();
                    return;
                }
            }

            previousTurn = activeTeam;
        };

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged += _freezeTurnListener;
    }

    public void RemoveFreeze()
    {
        if (!isFrozen) return;

        isFrozen = false;
        frozenOwnerTurnsRemaining = 0;

        if (_sr != null)
            _sr.color = _normalBaseColor;
        HideFrozenMarker();

        if (_freezeTurnListener != null && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged -= _freezeTurnListener;
            _freezeTurnListener = null;
        }
    }
    private void ShowFrozenMarker()
    {
        if (frozenSquareMarkerPrefab == null || _frozenSquareMarker != null)
            return;

        Vector3 worldPos = BoardInitializer.Instance != null
            ? BoardInitializer.Instance.GetWorldPosition(currentCell)
            : transform.position;

        _frozenSquareMarker = Instantiate(
            frozenSquareMarkerPrefab,
            worldPos,
            Quaternion.identity
        );

        // Keep marker attached to this piece.
        _frozenSquareMarker.transform.SetParent(
            transform,
            worldPositionStays: true
        );

        // Keep it centered on the board square.
        _frozenSquareMarker.transform.position = worldPos;

        // IMPORTANT:
        // Do NOT override sortingLayer / sortingOrder here.
        // The prefab's SpriteRenderer settings control rendering,
        // just like the Explosive Trap marker.
    }
    private void HideFrozenMarker()
    {
        if (_frozenSquareMarker != null)
        {
            Destroy(_frozenSquareMarker);
            _frozenSquareMarker = null;
        }
    }
    public override void OnDestroy()
    {
        base.OnDestroy();

        HideFrozenMarker();

        if (_turnListener != null && TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= _turnListener;

        if (_freezeTurnListener != null && TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= _freezeTurnListener;

        if (_stunTurnListener != null &&
            TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged -=
                _stunTurnListener;

            _stunTurnListener = null;
        }

        if (_stunnedMarker != null)
        {
            Destroy(_stunnedMarker);
            _stunnedMarker = null;
        }
    }

    void OnMouseEnter()
    {
        if (_sr != null) _sr.color = hoverColor;
    }

    void OnMouseExit()
    {
        if (_sr != null) _sr.color = _baseColor;
    }
    void OnMouseDown()
    {
        if (ChessBoard.Instance.gameOver) return;
        if (isStunned)
        {
            Debug.Log($"{name} is stunned and cannot move.");

            canDrag = false;
            isDragging = false;
            SnapBackToCurrentCell();

            return;
        }
        if (isFrozen)
        {
            Debug.Log($"{name} is frozen and cannot move.");
            return;
        }

        if (!TurnManager.Instance.IsPlayersTurn(team))
        {
            canDrag = false;
            isDragging = false;
            return;
        }

        canDrag = true;
        isDragging = true;
        originalPosition = transform.position;
        offset = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        offset.z = 0;
    }

    void OnMouseDrag()
    {
        if (!isDragging || !canDrag || ChessBoard.Instance.gameOver) return;

        if (isFrozen || isStunned)
        {
            isDragging = false;
            canDrag = false;
            SnapBackToCurrentCell();
            return;
        }
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        transform.position = mousePos + offset;
    }
    void OnMouseUp()
    {
        // =========================================================
        // STATUS CHECKS
        // =========================================================

        if (isFrozen || isStunned)
        {
            isDragging = false;
            canDrag = false;
            SnapBackToCurrentCell();
            return;
        }
        if (!isDragging || ChessBoard.Instance.gameOver || !canDrag) return;
        isDragging = false;
        canDrag = false;
        if (_sr != null) _sr.color = _baseColor;


        // =========================================================
        // TARGET SQUARE
        // =========================================================

        Vector3 snappedPosition = SnapToGrid(transform.position);
        Vector2Int newCell = WorldToCell(snappedPosition);
        Vector2Int fromCell = currentCell;

        // =========================================================
        // VALIDATE MOVE
        // =========================================================
        if (!IsValidMove(snappedPosition))
        {
            transform.position = originalPosition;
            return;
        }

        Vector2Int oldCell = currentCell;

        // =========================================================
        // 1. EN PASSANT CAPTURE
        // =========================================================

        if (pieceType == PieceType.Pawn)
        {
            int forward =
                team == TeamColor.White ? 1 : -1;

            bool diagonal =
                Mathf.Abs(newCell.x - oldCell.x) == 1;

            bool forwardStep =
                newCell.y - oldCell.y == forward;

            if (diagonal && forwardStep)
            {
                ChessPiece targetOnNewCell =
                    ChessBoard.Instance.GetPieceAt(newCell);

                // Empty diagonal destination could be en passant.
                if (targetOnNewCell == null)
                {
                    var board = ChessBoard.Instance;

                    if (board.enPassantTarget.x >= 0 &&
                        board.enPassantTarget == newCell)
                    {
                        Vector2Int victimCell =
                            new Vector2Int(
                                newCell.x,
                                oldCell.y
                            );

                        ChessPiece victim =
                            ChessBoard.Instance.GetPieceAt(victimCell);

                        if (victim == null ||
                            victim.team == team ||
                            victim.pieceType != PieceType.Pawn)
                        {
                            transform.position = originalPosition;
                            return;
                        }

                        // Divine Protection / Freeze protection
                        if (victim.IsDivinelyProtected ||
                            victim.IsFrozen)
                        {
                            transform.position = originalPosition;
                            return;
                        }

                        ChessBoard.Instance.CapturePiece(victimCell);
                    }
                }
            }
        }

        // 2) Normal capture (occupied target square)
        ChessPiece directTarget = ChessBoard.Instance.GetPieceAt(newCell);
        if (directTarget != null && directTarget.team != team)
        {
            if (directTarget.IsDivinelyProtected || directTarget.IsFrozen)
            {
                transform.position = originalPosition;
                return;
            }

            ChessBoard.Instance.CapturePiece(newCell);
        }

        // A trap must be checked for EVERY legal arrival, not only captures.
        bool explosiveTrapTriggered = ChessBoard.Instance.TryConsumeEnemyExplosiveTrap(
            newCell,
            team,
            out _
        );


        if (explosiveTrapTriggered)
        {
            // First land the piece on the trap square.
            ChessBoard.Instance.MovePiece(oldCell, newCell);

            currentCell = newCell;
            hasMoved = true;
            transform.position = snappedPosition;

            // An exploded pawn cannot create a new en-passant window.
            ChessBoard.Instance.ClearEnPassant();

            // Explosion -> shatter -> normal CapturePiece bookkeeping.
            StartCoroutine(ResolveExplosiveTrapOffline(newCell));

            return;
        }
        // PROMOTION
        if (pieceType == PieceType.Pawn &&
            Pawn.ShouldPromote(newCell, team))
        {
            transform.position = snappedPosition;
            currentCell = newCell;

            ChessBoard.Instance.pawnToPromote = this;
            ChessBoard.Instance.TriggerPromotion(this);

            return;
        }

        // (Safety: if a NetworkManager exists but not listening, this won't early-return)
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm && nm.IsListening)
        {
            if (NetPlayer.Local != null && NetPlayer.Local.CanAct())
                NetPlayer.Local.TryRequestMove(this.Id, newCell);
            else
                transform.position = originalPosition;
            return;
        }

        // 4) Finalize the move locally
        ChessBoard.Instance.MovePiece(oldCell, newCell);
        currentCell = newCell;
        hasMoved = true;
        transform.position = snappedPosition;

        // 5) Castling rook shift (unchanged)
        if (pieceType == PieceType.King && Mathf.Abs(newCell.x - oldCell.x) == 2)
        {
            bool kingSide = newCell.x > oldCell.x;
            int rookFromX = kingSide ? 7 : 0;
            int rookToX = kingSide ? (newCell.x - 1) : (newCell.x + 1);

            Vector2Int rookFrom = new Vector2Int(rookFromX, newCell.y);
            Vector2Int rookTo = new Vector2Int(rookToX, newCell.y);

            ChessPiece rook = ChessBoard.Instance.GetPieceAt(rookFrom);
            if (rook != null && rook.pieceType == PieceType.Rook && rook.team == team)
            {
                ChessBoard.Instance.MovePiece(rookFrom, rookTo);
                rook.currentCell = rookTo;
                rook.hasMoved = true;
                rook.transform.position = BoardInitializer.Instance.GetWorldPosition(rookTo);
            }
        }

        // 6) Maintain En Passant window OFFLINE (mirror server logic)
        ChessBoard.Instance.ClearEnPassant();
        if (pieceType == PieceType.Pawn && Mathf.Abs(newCell.y - oldCell.y) == 2 && newCell.x == oldCell.x)
        {
            var mid = new Vector2Int(oldCell.x, (oldCell.y + newCell.y) / 2); // passed square
            ChessBoard.Instance.enPassantTarget = mid;
            ChessBoard.Instance.enPassantPawnId = this.Id;
        }

        // 7) SFX + turn advance
        if (audioSource != null && moveClip != null)
            audioSource.PlayOneShot(moveClip);

        TurnManager.Instance.NextTurn();
    }

    Vector3 SnapToGrid(Vector3 rawPosition)
    {
        float cellSize = 0.5f; // Assuming 128px sprites with 256 PPU
        float x = Mathf.Floor(rawPosition.x / cellSize) * cellSize + cellSize / 2f;
        float y = Mathf.Floor(rawPosition.y / cellSize) * cellSize + cellSize / 2f;
        return new Vector3(x, y, 0f);
    }

    private IEnumerator ResolveExplosiveTrapOffline(Vector2Int trapCell)
    {
        // Prevent any additional dragging while the animation resolves.
        canDrag = false;
        isDragging = false;

        // Remove the visible bomb marker.
        ChessBoard.Instance.HideExplosiveTrapMarker(trapCell);

        // -----------------------------
        // 1. EXPLOSION
        // -----------------------------
        ChessBoard.Instance.PlayExplosiveTrapEffect(trapCell);

        // Give the explosion a tiny visual head start.
        yield return new WaitForSeconds(0.05f);

        // -----------------------------
        // 2. SHATTER THE CHESS PIECE
        // -----------------------------
        if (PieceShatterVFX.Instance != null)
        {
            PieceShatterVFX.Instance.Play(this);
        }
        else
        {
            Debug.LogWarning(
                "[TRAP] PieceShatterVFX is missing from the scene."
            );

            // Fallback: at least hide the original sprite.
            SpriteRenderer sr = GetComponent<SpriteRenderer>();

            if (sr != null)
                sr.enabled = false;
        }

        // Allow the shards to fly before destroying the actual ChessPiece.
        yield return new WaitForSeconds(0.45f);

        // -----------------------------
        // 3. EXISTING CAPTURE LOGIC
        // -----------------------------
        ChessBoard.Instance.CapturePiece(trapCell);

        // CapturePiece handles:
        // - graveyard
        // - resurrection information
        // - king/game over
        // - unregistering
        // - destroying the actual piece

        // -----------------------------
        // 4. CONTINUE THE TURN
        // -----------------------------
        if (!ChessBoard.Instance.gameOver)
            TurnManager.Instance.NextTurn();
    }

    bool IsValidMove(Vector3 targetPosition)
    {
        Vector2Int targetCell = WorldToCell(targetPosition);

        switch (pieceType)
        {
            case PieceType.Pawn:
                return Pawn.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    hasMoved,
                    ChessBoard.Instance.GetPieceAt
                );
            case PieceType.Knight:     // this case is pieceType knight and will activate the IsValidMove embedded in the knight script 
                return Knight.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt 
                );

            case PieceType.Rook:
                return Rook.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt
                );
            case PieceType.Bishop:
                return Bishop.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt
                );
            case PieceType.Queen:
                return Queen.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt
                );
            case PieceType.King:
                return King.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt
                );

            default:
                return false;
        }
    }

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        float cellSize = 0.5f;
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int y = Mathf.FloorToInt(worldPos.y / cellSize);
        return new Vector2Int(x, y);
    }

    public void MarkAsResurrected()                 //currently used for visual coloring only
    {
        isResurrected = true;
        if (_sr != null)
        {
            // Soft yellow tint (permanent)
            _sr.color = new Color(1f, 0.95f, 0.5f, 1f);
        }
    }

    public void TryMoveFromTap(Vector2Int targetCell)
    {
        if (ChessBoard.Instance.gameOver)
            return;

        // Keep existing status restrictions
        if (isFrozen || isStunned)
        {
            SnapBackToCurrentCell();
            return;
        }

        // Must be this team's turn
        if (!TurnManager.Instance.IsPlayersTurn(team))
            return;

        // Remember where we were, exactly like drag movement
        originalPosition = transform.position;

        // Pretend the piece was dragged onto the tapped square.
        Vector3 targetWorld =
            BoardInitializer.Instance.GetWorldPosition(targetCell);

        transform.position = targetWorld;

        canDrag = true;
        isDragging = true;

        // IMPORTANT:
        // Use the EXISTING movement pipeline.
        OnMouseUp();
    }
    void SnapBackToCurrentCell()
    {
        Vector3 p = BoardInitializer.Instance
            ? BoardInitializer.Instance.GetWorldPosition(currentCell)
            : transform.position; // fallback
        transform.position = p;
    }
}


