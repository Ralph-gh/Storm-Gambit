using UnityEngine;
using System;

public static class King
{
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        // IMPORTANT: signed deltas
        int dx = targetCell.x - currentCell.x;
        int dy = targetCell.y - currentCell.y;

        // --- Castling (2 squares horizontally on same rank) ---
        if (dy == 0 && Mathf.Abs(dx) == 2)
        {
            // King must exist and must not have moved
            ChessPiece king = getPieceAt(currentCell);
            if (king == null || king.hasMoved) return false;

            bool kingSide = dx > 0;
            int rookX = kingSide ? 7 : 0;
            Vector2Int rookPos = new Vector2Int(rookX, currentCell.y);

            ChessPiece rook = getPieceAt(rookPos);
            if (rook == null || rook.pieceType != PieceType.Rook || rook.team != team || rook.hasMoved)
                return false;

            // Target square must be empty
            if (getPieceAt(targetCell) != null) return false;

            // Squares between king and rook must be empty
            int step = kingSide ? 1 : -1;
            for (int x = currentCell.x + step; x != rookPos.x; x += step)
            {
                if (getPieceAt(new Vector2Int(x, currentCell.y)) != null)
                    return false; // queenside includes x=3,2,1; kingside includes x=5 only
            }

            // (Skipping "in/through check" tests for now)
            return true;
        }

        // --- Normal king move (1 square any direction) ---
        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1)
        {
            ChessPiece target = getPieceAt(targetCell);
            return target == null || target.team != team;
        }

        return false;
    }
}
