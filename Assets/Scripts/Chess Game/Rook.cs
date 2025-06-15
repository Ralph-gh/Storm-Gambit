using UnityEngine;
using System;
using System.Collections.Generic;

public static class Rook
{
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        // Movement must be in a straight line: same row or same column
        if (currentCell.x != targetCell.x && currentCell.y != targetCell.y)
            return false;

        // Determine movement direction
        int dx = Math.Sign(targetCell.x - currentCell.x);
        int dy = Math.Sign(targetCell.y - currentCell.y);

        Vector2Int check = currentCell;

        // Step toward target, checking each square
        while (true)
        {
            check += new Vector2Int(dx, dy);

            if (check == targetCell)
            {
                ChessPiece target = getPieceAt(targetCell);
                return target == null || target.team != team;
            }

            // If there's a piece in the way, block movement
            if (getPieceAt(check) != null)
                return false;
        }
    }
}
