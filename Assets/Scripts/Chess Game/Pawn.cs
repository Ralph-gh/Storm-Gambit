using UnityEngine;

public static class Pawn
{
    private static int Forward(TeamColor team) => (team == TeamColor.White) ? 1 : -1;

    /// <summary>
    /// Core check for en passant eligibility on a diagonal step into an empty target.
    /// Requires ChessBoard.Instance.enPassantTarget (and optionally enPassantPawnId).
    /// </summary>
    private static bool IsEnPassantPossible(
        Vector2Int from,
        Vector2Int to,
        TeamColor team,
        System.Func<Vector2Int, ChessPiece> getPieceAt)
    {
        var board = ChessBoard.Instance;
        if (board == null) return false;

        // Must match the board-advertised EP target
        if (board.enPassantTarget.x < 0 || board.enPassantTarget != to) return false;

        // The victim pawn sits horizontally adjacent on the "from" rank
        var victimCell = new Vector2Int(to.x, from.y);
        var victim = getPieceAt(victimCell);
        if (victim == null || victim.pieceType != PieceType.Pawn || victim.team == team) return false;

        // Optional strict check against the tracked pawn id (if you use it)
        if (board.enPassantPawnId >= 0 && victim.Id != board.enPassantPawnId) return false;

        return true;
    }

    // ------ Overload WITHOUT hasVictim flag ------
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        bool hasMoved,
        System.Func<Vector2Int, ChessPiece> getPieceAt)
    {
        int direction = Forward(team);
        int deltaX = targetCell.x - currentCell.x;
        int deltaY = targetCell.y - currentCell.y;

        // Forward moves (non-capturing)
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
            return false;
        }

        // Diagonal step (captures or en passant)
        if (Mathf.Abs(deltaX) == 1 && deltaY == direction)
        {
            var target = getPieceAt(targetCell);
            if (target != null && target.team != team)
                return true; // normal capture

            // en passant: diagonal into empty square that matches board EP target
            if (target == null && IsEnPassantPossible(currentCell, targetCell, team, getPieceAt))
                return true;

            return false;
        }

        return false;
    }

    // ------ Overload WITH hasVictim flag (useful when caller already knows target occupancy) ------
    public static bool IsValidMove(
        Vector2Int currentCell,
        Vector2Int targetCell,
        TeamColor team,
        bool hasMoved,
        System.Func<Vector2Int, ChessPiece> getPieceAt,
        bool hasVictim)
    {
        int direction = Forward(team);
        int deltaX = targetCell.x - currentCell.x;
        int deltaY = targetCell.y - currentCell.y;

        // Forward moves (non-capturing)
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
            return false;
        }

        // Diagonal step (captures or en passant)
        if (Mathf.Abs(deltaX) == 1 && deltaY == direction)
        {
            // If caller already detected a victim on the target, it's a normal capture
            if (hasVictim) return true;

            // Otherwise, if target is empty, check for en passant
            var target = getPieceAt(targetCell);
            if (target == null && IsEnPassantPossible(currentCell, targetCell, team, getPieceAt))
                return true;

            return false;
        }

        return false;
    }

    public static bool ShouldPromote(Vector2Int targetCell, TeamColor team)
    {
        return (team == TeamColor.White && targetCell.y == 7)
            || (team == TeamColor.Black && targetCell.y == 0);
    }
}
