using UnityEngine;
using System;

public static class Bishop
{
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        int dx = targetCell.x - currentCell.x;
        int dy = targetCell.y - currentCell.y;

        // Must move diagonally (equal absolute distance)
        if (Mathf.Abs(dx) != Mathf.Abs(dy))
            return false;

        // Determine direction: ±1 for x and y
        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        Vector2Int check = currentCell;

        // Step toward target, checking every square
        while (true)
        {
            check += new Vector2Int(stepX, stepY);

            if (check == targetCell)
            {
                ChessPiece target = getPieceAt(targetCell);
                return target == null || target.team != team;
            }

            if (getPieceAt(check) != null)
                return false;
        }
    }
}
