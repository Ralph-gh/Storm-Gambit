using UnityEngine;
using System;
using System.Collections.Generic;

public static class Knight
{
    public static bool IsValidMove(Vector2Int currentCell, Vector2Int targetCell, TeamColor team, Func<Vector2Int, ChessPiece> getPieceAt)
    {
        int dx = Mathf.Abs(targetCell.x - currentCell.x);
        int dy = Mathf.Abs(targetCell.y - currentCell.y);

        // Must move in an L shape: 2 + 1 in either direction
        if ((dx == 2 && dy == 1) || (dx == 1 && dy == 2))
        {
            ChessPiece target = getPieceAt(targetCell);
            // Can move if target is empty or enemy
            return target == null || target.team != team;
        }

        return false;
    }
}
