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
    string winner = "Me";// a sense of humour does not hurt a coder
    public GameObject promotionUI;
    public ChessPiece pawnToPromote;
    public BoardInitializer initializer;
    public PromotionSelector promotionSelector;
    public Graveyard graveyard = new();

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
    }

    public void MovePiece(Vector2Int from, Vector2Int to)
    {
        board[to.x, to.y] = board[from.x, from.y];
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

}
