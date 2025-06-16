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
            capturedPieces.Add(target);
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
            
            
            
           
            // Destroy the game object
            if (audioSource != null && captureClip != null)
                audioSource.PlayOneShot(captureClip);
            capturedPieces.Add(target); // Optionally: track captured pieces in a list
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

}
