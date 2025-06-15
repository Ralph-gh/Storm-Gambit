using UnityEngine;
using System;

public static class Queen
{
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        // Reuse Bishop and Rook logic
        return Bishop.IsValidMove(currentCell, targetCell, team, getPieceAt)
            || Rook.IsValidMove(currentCell, targetCell, team, getPieceAt);
    }
}
