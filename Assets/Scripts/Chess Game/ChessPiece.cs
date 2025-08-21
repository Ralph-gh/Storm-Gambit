using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum TeamColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King}
public class ChessPiece : MonoBehaviour
{
    
    public TeamColor team;
    public PieceType pieceType;
    public bool hasMoved= false;
    public Vector2Int currentCell;
    public AudioClip moveClip;
    public AudioSource audioSource;
    private bool canDrag = true; //  new flag
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

        // Optional: add a glow/icon here
        // e.g. GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.8f);

        // When it becomes the owner's turn again, the opponent's turn has finished -> remove
        _turnListener = (TeamColor activeTeam) =>
        {
            if (activeTeam == protectionOwnerTeam)
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

        // Optional: remove glow/icon here
        // e.g. GetComponent<SpriteRenderer>().color = Color.white;
    }

    private void OnDestroy()
    {
        if (_turnListener != null && TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= _turnListener;
    }
    void OnMouseDown()
    {
        if (ChessBoard.Instance.gameOver) return;
        if (!TurnManager.Instance.IsPlayersTurn(team))
        {
            canDrag = false;
            isDragging = false; // critical line to avoid pieces dragging by mistake
            return;
        }
        canDrag = true;
        isDragging = true;
        originalPosition = transform.position;
        offset = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        offset.z = 0; // lock to 2D plane
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (!isDragging || !canDrag || ChessBoard.Instance.gameOver)
            return;
        if (!TurnManager.Instance.IsPlayersTurn(team)) return; // double protection
        {
            
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f; // Force to 2D layer
            transform.position = mousePos + offset;
        }
    }

    void OnMouseUp()
    {
       

        // Cancel interaction early if dragging was denied or game is over
        if (!isDragging || ChessBoard.Instance.gameOver || !canDrag)
        {
            
            return;
        }
        isDragging = false;
        canDrag = false;

        // Turn enforcement
        if (!TurnManager.Instance.IsPlayersTurn(team))
        {
            transform.position = originalPosition;
            return;
        }

        Vector3 snappedPosition = SnapToGrid(transform.position);
        Vector2Int newCell = WorldToCell(snappedPosition);

        if (!IsValidMove(snappedPosition))
        {
            transform.position = originalPosition;
            return;
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

        // Finalize move
        ChessBoard.Instance.MovePiece(currentCell, newCell);
        currentCell = newCell;
        hasMoved = true;
        transform.position = snappedPosition;

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


            // Add other piece cases later...

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
}
