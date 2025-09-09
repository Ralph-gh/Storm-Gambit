using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ChessBoard : MonoBehaviour
{
    public static ChessBoard Instance;
    public List<ChessPiece> capturedPieces = new List<ChessPiece>();// List to track captured pieces
    private ChessPiece[,] board = new ChessPiece[8, 8];
    public bool resurrectionAllowed = false;
    public bool gameOver = false;
    public GameObject victoryScreen;
    public TMPro.TextMeshProUGUI victoryText; // or UnityEngine.UI.Text
    public AudioClip victoryClip;
    public AudioClip captureClip;
    public AudioSource audioSource;
    //string winner = "Me"; for winner testing
    public GameObject promotionUI;
    public ChessPiece pawnToPromote;
    public BoardInitializer initializer;
    public PromotionSelector promotionSelector;
    public Graveyard graveyard = new();
    private Dictionary<int, ChessPiece> idLookup = new Dictionary<int, ChessPiece>();//Dictionnary lookup for networking
    public void RegisterPiece(ChessPiece p) { if (p) idLookup[p.Id] = p; }
    public void UnregisterPiece(ChessPiece p) { if (p) idLookup.Remove(p.Id); }


    public void TriggerPromotion(ChessPiece pawn)
    {
        pawnToPromote = pawn;
        if (promotionSelector != null)
        {
            promotionSelector.Show(pawn.transform.position, pawn.currentCell, pawn.team);
        }
        else
        {
            Debug.LogWarning("Promotion UI is not assigned. Auto-promoting to Queen.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void PlacePiece(ChessPiece piece, Vector2Int position)
    {
        board[position.x, position.y] = piece;
        idLookup[piece.Id] = piece;
    }

    public void MovePiece(Vector2Int from, Vector2Int to)
    {
        var p = board[from.x, from.y];
        board[to.x, to.y] = p;
        board[from.x, from.y] = null;
    }

    public ChessPiece GetPieceAt(Vector2Int position)
    {
        if (position.x < 0 || position.x >= 8 || position.y < 0 || position.y >= 8)
            return null;

        return board[position.x, position.y];
    }

    public void CapturePiece(Vector2Int targetPosition)
    {
        if (gameOver) return; // prevent multiple endings

        ChessPiece target = GetPieceAt(targetPosition);

        if (target != null) 
        {
            // Clear from board array
            board[targetPosition.x, targetPosition.y] = null;
            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                target.pieceSprite = sr.sprite;
                Debug.Log($"[CAPTURE] Stored sprite: {target.pieceSprite.name}");
            }
            else
            {
                Debug.LogWarning("[CAPTURE]  SpriteRenderer missing on target.");
            }
            if (target.pieceType == PieceType.King && !resurrectionAllowed)
            {
                gameOver = true;
                string winner = target.team == TeamColor.White ? "Black Wins!" : "White Wins!";
                Debug.Log($"Game Over — {winner}");
                
                if (victoryScreen != null)
                {
                    victoryScreen.SetActive(true);
                    victoryText.text = winner;
                    if (victoryClip != null && audioSource != null)
                        audioSource.PlayOneShot(victoryClip);
                }
            }
            // Backup the sprite before destruction
            Sprite savedSprite = target.pieceSprite;
            target.pieceSprite = savedSprite; // Redundant in theory, but keeps the reference alive
            CapturedPieceData data = new CapturedPieceData(target);
            data.originalPosition = target.startingCell; // add this to the constructor if you prefer
            graveyard.AddPiece(data); // CORRECT
            
            Debug.Log($"[CAPTURE] Captured {target.pieceType}, sprite: {target.pieceSprite?.name}");
            // Destroy the game object
            if (audioSource != null && captureClip != null)
                audioSource.PlayOneShot(captureClip);
            GameObject.Destroy(target.gameObject); // capturedPieces.Add(target);
        }
    }

    public List<ChessPiece> GetCapturedPieces(TeamColor team)
    {
        return capturedPieces.FindAll(p => p.team == team);
    }

    public bool HasCapturedPieces(TeamColor team)
    {
        return GetCapturedPieces(team).Count > 0;
    }

    public void RemoveCapturedPiece(TeamColor team, ChessPiece piece)
    {
        capturedPieces.Remove(piece);
    }

    public Vector2Int FindNearestAvailableSquare(Vector2Int origin)
    {
        Vector2Int[] directions = {
        Vector2Int.zero,
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1)
    };

        foreach (var dir in directions)
        {
            Vector2Int candidate = origin + dir;
            if (IsInsideBoard(candidate) && GetPieceAt(candidate) == null)
                return candidate;
        }

        return new Vector2Int(-1, -1); // No valid square
    }
    public bool IsInsideBoard(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < 8 && cell.y >= 0 && cell.y < 8;
    }
    public class CapturedPieceData
    {
        public PieceType pieceType;
        public TeamColor team;
        public GameObject originalPrefab;
        public Vector2Int originalPosition;
        public Sprite pieceSprite;

        public CapturedPieceData(ChessPiece piece)
        {
            pieceType = piece.pieceType;
            team = piece.team;
            originalPrefab = piece.originalPrefab;
            pieceSprite = piece.pieceSprite;
            originalPosition = piece.startingCell;
        }
    }

    public class Graveyard
    {
        public List<CapturedPieceData> whiteCaptured = new();
        public List<CapturedPieceData> blackCaptured = new();

        public void AddPiece(CapturedPieceData data)
        {
            if (data.team == TeamColor.White)
                whiteCaptured.Add(data);
            else
                blackCaptured.Add(data);
        }

        public void RemoveCapturedPiece(CapturedPieceData data)
        {
            if (data.team == TeamColor.White)
                whiteCaptured.Remove(data);
            else
                blackCaptured.Remove(data);
        }

        public List<CapturedPieceData> GetCapturedByTeam(TeamColor team)
        {
            return team == TeamColor.White ? whiteCaptured : blackCaptured;
        }
    }

    // Authoritative server-side apply (no input here)
    public void ExecuteMoveServer(ChessPiece piece, Vector2Int to)
    {
        // capture if opponent on target
        var capture = GetPieceAt(to);
        if (capture != null && capture.team != piece.team)
        {
            // keep your capture side-effects minimal on server
            // board bookkeeping only; visual destroy is done on clients in ApplyMoveClientRpc
            board[to.x, to.y] = null;
        }

        // move board state server-side
        Vector2Int from = piece.currentCell;
        MovePiece(from, to);
        piece.currentCell = to;
        piece.hasMoved = true;
    }

    // Client-side visuals only (called by RPC)
    // === NEW: rules check wrapper (reuses your existing validators) ===
public bool IsLegalMove(ChessPiece piece, Vector2Int to)
    {
        switch (piece.pieceType)
        {
            case PieceType.Pawn:
                return Pawn.IsValidMove(piece.currentCell, to, piece.team, piece.hasMoved, GetPieceAt);
            case PieceType.Knight:
                return Knight.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Bishop:
                return Bishop.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Rook:
                return Rook.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Queen:
                return Queen.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.King:
                return King.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            default:
                return false;
        }
    }



    // === NEW: local visual+board apply (used by ClientRpc) ===
    public void MovePieceLocal(ChessPiece piece, Vector2Int to)
    {
        var from = piece.currentCell;
        board[to.x, to.y] = piece;
        board[from.x, from.y] = null;
        piece.currentCell = to;
        piece.hasMoved = true;
        piece.transform.position = BoardInitializer.Instance
            ? BoardInitializer.Instance.GetWorldPosition(to)
            : new Vector3((to.x + 0.5f) * 0.5f, (to.y + 0.5f) * 0.5f, 0f); // fallback
    }


    // === NEW: remove on clients (used when ApplyMoveClientRpc says a capture happened) ===
    public void RemovePieceLocal(ChessPiece piece)
    {
        board[piece.currentCell.x, piece.currentCell.y] = null;
        idLookup.Remove(piece.Id);
        if (piece) Destroy(piece.gameObject);
    }


    
    public ChessPiece GetPieceById(int id)
    {
        ChessPiece p;
        return idLookup.TryGetValue(id, out p) ? p : null;
    }

    public void RebuildIndexFromScene()
    {
        idLookup.Clear();
        var all = FindObjectsByType<ChessPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in all) idLookup[p.Id] = p;
    }
}
