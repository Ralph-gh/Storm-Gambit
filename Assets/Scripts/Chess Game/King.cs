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
        int dx = Mathf.Abs(targetCell.x - currentCell.x);
        int dy = Mathf.Abs(targetCell.y - currentCell.y);

        // King can only move 1 square in any direction
        if (dx > 1 || dy > 1)
            return false;

        ChessPiece target = getPieceAt(targetCell);
        return target == null || target.team != team;
    }
}
