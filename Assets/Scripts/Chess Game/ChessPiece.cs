using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum TeamColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King}
public class ChessPiece : MonoBehaviour
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

    //Network
    

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;
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

    private void OnDestroy()
    {
        if (_turnListener != null && TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= _turnListener;
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

        bool isNet = Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;

        // Multiplayer: only the local owner of this side AND only on their turn may drag
        if (isNet)
        {
            if (NetPlayer.Local == null || NetPlayer.Local.Side.Value != team || !NetPlayer.Local.CanAct())
            {
                canDrag = false;
                isDragging = false;
                SnapBackToCurrentCell(); // snap back to center during multiplayer
                return;
            }
        }
        // Offline: same as before
        else if (!TurnManager.Instance.IsPlayersTurn(team))
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

        bool isNet = Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (isNet)
        {
            if (NetPlayer.Local == null || NetPlayer.Local.Side.Value != team || !NetPlayer.Local.CanAct()) return;
        }
        else
        {
            if (!TurnManager.Instance.IsPlayersTurn(team)) return;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        transform.position = mousePos + offset;
    }
    void OnMouseUp()
    {


        if (!isDragging || ChessBoard.Instance.gameOver || !canDrag) return;
        isDragging = false;
        canDrag = false;

        if (_sr != null) _sr.color = _baseColor;

        bool isNet = Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;

        Vector3 snappedPosition = SnapToGrid(transform.position);
        Vector2Int newCell = WorldToCell(snappedPosition);

        

        if (!IsValidMove(snappedPosition))
        {
            transform.position = originalPosition;
            return;
        }

        // --- NETWORKED PATH: request server, but DO NOT stick locally ---
        if (isNet)
        {
            // Optional: block captures on divinely protected targets here and just snap back
            // Ask server; immediately snap back so only server decides the final position

            if (NetPlayer.Local != null && NetPlayer.Local.CanAct() && NetPlayer.Local.Side.Value == team)
                NetPlayer.Local.TryRequestMove(this.Id, newCell);

            SnapBackToCurrentCell();   // <- prevents floating overlays
            return; // wait for ApplyMoveClientRpc to move both boards
        }

        // Capture logic (before promotion)
        ChessPiece target = ChessBoard.Instance.GetPieceAt(newCell);
        if (target != null && target.team != team)
        {
            // NEW: block capture if target has Divine Protection
            if (target.IsDivinelyProtected)
            {
                // invalid move: snap back and let the player try another move
                transform.position = originalPosition;
                return;
            }

            ChessBoard.Instance.CapturePiece(newCell);
        }

        // Promotion logic (AFTER capture)
        if (pieceType == PieceType.Pawn && Pawn.ShouldPromote(newCell, team))
        {
            transform.position = snappedPosition;
            currentCell = newCell;
            ChessBoard.Instance.pawnToPromote = this;
            ChessBoard.Instance.TriggerPromotion(this);
            
            return; // stop here to avoid switching turns before promotion
        }
        // If we’re in a networked session, send the request via RPC and STOP local apply.
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm && nm.IsListening)
        {
            if (NetPlayer.Local != null && NetPlayer.Local.CanAct())
            {
                NetPlayer.Local.TryRequestMove(this.Id, newCell);
            }
            else
            {
                // not your turn or not owned, snap back
                transform.position = originalPosition;
            }
            return; // leave; server will validate and broadcast via ClientRpc
        }
        // Finalize move
        Vector2Int oldCell = currentCell;  
        ChessBoard.Instance.MovePiece(currentCell, newCell);
        currentCell = newCell;
        hasMoved = true;
        transform.position = snappedPosition;

        // If this was a king castling move, also move the rook
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
                // Update board array
                ChessBoard.Instance.MovePiece(rookFrom, rookTo);

                // Update rook component and transform
                rook.currentCell = rookTo;
                rook.hasMoved = true;
                rook.transform.position = BoardInitializer.Instance.GetWorldPosition(rookTo);
            }
        }

        if (audioSource != null && moveClip != null)
            audioSource.PlayOneShot(moveClip);

        TurnManager.Instance.NextTurn();

        // Now reset drag flag
        
    }

    Vector3 SnapToGrid(Vector3 rawPosition)
    {
        float cellSize = 0.5f; // Assuming 128px sprites with 256 PPU
        float x = Mathf.Floor(rawPosition.x / cellSize) * cellSize + cellSize / 2f;
        float y = Mathf.Floor(rawPosition.y / cellSize) * cellSize + cellSize / 2f;
        return new Vector3(x, y, 0f);
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
    void SnapBackToCurrentCell()
    {
        Vector3 p = BoardInitializer.Instance
            ? BoardInitializer.Instance.GetWorldPosition(currentCell)
            : transform.position; // fallback
        transform.position = p;
    }
}


