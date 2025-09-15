using UnityEngine;
using System.Collections.Generic;

public static class Pawn
{
   
    public static bool IsValidMove(Vector2Int currentCell, Vector2Int targetCell, TeamColor team, bool hasMoved, System.Func<Vector2Int, ChessPiece> getPieceAt)
    {
        int direction = (team == TeamColor.White) ? 1 : -1;
        int deltaX = targetCell.x - currentCell.x;
        int deltaY = targetCell.y - currentCell.y;

        // Forward move
        if (deltaX == 0)
        {
            if (deltaY == direction)
            {
                // Single forward, must be empty
                return getPieceAt(targetCell) == null;
            }
            else if (deltaY == 2 * direction && !hasMoved)
            {
                // Double forward, both squares must be empty
                Vector2Int midCell = new Vector2Int(currentCell.x, currentCell.y + direction);
                return getPieceAt(midCell) == null && getPieceAt(targetCell) == null;
            }
        }

        // Diagonal capture
        if (Mathf.Abs(deltaX) == 1 && deltaY == direction)
        {
            ChessPiece target = getPieceAt(targetCell);
            return target != null && target.team != team;
        }
  
        return false;
    }

    public static bool IsValidMove(
    Vector2Int currentCell,
    Vector2Int targetCell,
    TeamColor team,
    bool hasMoved,
    System.Func<Vector2Int, ChessPiece> getPieceAt,
    bool hasVictim)
    {
        int direction = (team == TeamColor.White) ? 1 : -1;
        int deltaX = targetCell.x - currentCell.x;
        int deltaY = targetCell.y - currentCell.y;

        // Forward move
        if (deltaX == 0)
        {
            if (deltaY == direction)
            {
                // Single forward, must be empty
                return getPieceAt(targetCell) == null;
            }
            else if (deltaY == 2 * direction && !hasMoved)
            {
                // Double forward, both squares must be empty
                Vector2Int midCell = new Vector2Int(currentCell.x, currentCell.y + direction);
                return getPieceAt(midCell) == null && getPieceAt(targetCell) == null;
            }
        }

        // Diagonal capture
        if (Mathf.Abs(deltaX) == 1 && deltaY == direction)
        {
            // If host already checked and saw victim, trust that
            if (hasVictim) return true;

            ChessPiece target = getPieceAt(targetCell);
            return target != null && target.team != team;
        }

        return false;
    }
    public static bool ShouldPromote(Vector2Int targetCell, TeamColor team)
    {
        return (team == TeamColor.White && targetCell.y == 7)
            || (team == TeamColor.Black && targetCell.y == 0);
    }
}