using System.Collections.Generic;
using UnityEngine;

public class ChessBoard : MonoBehaviour
{
    public static ChessBoard Instance;
    public List<ChessPiece> capturedPieces = new List<ChessPiece>();// List to track captured pieces
    private ChessPiece[,] board = new ChessPiece[8, 8];

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
        ChessPiece target = GetPieceAt(targetPosition);
        if (target != null)
        {
            // Clear from board array
            board[targetPosition.x, targetPosition.y] = null;

            // Optionally: track captured pieces in a list
            // capturedPieces.Add(target);

            // Destroy the game object
            capturedPieces.Add(target);
            GameObject.Destroy(target.gameObject);
        }
    }
}
