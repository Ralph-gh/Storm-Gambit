using UnityEngine;

public static class ChessAttackEvaluator
{
    // =========================================================
    // FIND KING
    // =========================================================

    public static ChessPiece FindKing(TeamColor team)
    {
        if (ChessBoard.Instance == null)
        {
            Debug.LogWarning("[CHECK] ChessBoard.Instance is null.");
            return null;
        }

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece =
                    ChessBoard.Instance.GetPieceAt(
                        new Vector2Int(x, y)
                    );

                if (piece == null)
                    continue;

                if (piece.team == team &&
                    piece.pieceType == PieceType.King)
                {
                    return piece;
                }
            }
        }

        return null;
    }

    // =========================================================
    // KING CHECK
    // =========================================================

    public static bool IsKingInCheck(TeamColor team)
    {
        ChessPiece king = FindKing(team);

        if (king == null)
        {
            Debug.LogWarning(
                $"[CHECK] {team} king was not found."
            );

            return false;
        }

        TeamColor enemyTeam =
            team == TeamColor.White
                ? TeamColor.Black
                : TeamColor.White;

        return IsSquareAttacked(
            king.currentCell,
            enemyTeam
        );
    }

    // =========================================================
    // SQUARE ATTACK
    // =========================================================

    public static bool IsSquareAttacked(
        Vector2Int square,
        TeamColor byTeam)
    {
        return TryGetAttacker(
            square,
            byTeam,
            out _
        );
    }

    // Useful later for:
    // - highlighting the checking piece
    // - check notifications
    // - debugging
    // - checkmate analysis
    public static bool TryGetAttacker(
        Vector2Int square,
        TeamColor byTeam,
        out ChessPiece attacker)
    {
        attacker = null;

        if (ChessBoard.Instance == null)
            return false;

        if (!ChessBoard.Instance.IsInsideBoard(square))
            return false;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece =
                    ChessBoard.Instance.GetPieceAt(
                        new Vector2Int(x, y)
                    );

                if (piece == null)
                    continue;

                if (piece.team != byTeam)
                    continue;

                if (CanPieceAttackSquare(piece, square))
                {
                    attacker = piece;
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    // RAW ATTACK RULES
    // =========================================================

    public static bool CanPieceAttackSquare(
        ChessPiece piece,
        Vector2Int target)
    {
        if (piece == null)
            return false;

        if (ChessBoard.Instance == null)
            return false;

        if (!ChessBoard.Instance.IsInsideBoard(target))
            return false;

        Vector2Int from = piece.currentCell;

        if (from == target)
            return false;

        int dx = target.x - from.x;
        int dy = target.y - from.y;

        switch (piece.pieceType)
        {
            // -------------------------------------------------
            // PAWN
            // Pawns ATTACK diagonally.
            // Do not use normal pawn movement here.
            // -------------------------------------------------

            case PieceType.Pawn:
                {
                    int direction =
                        piece.team == TeamColor.White
                            ? 1
                            : -1;

                    return
                        Mathf.Abs(dx) == 1 &&
                        dy == direction;
                }

            // -------------------------------------------------
            // KNIGHT
            // -------------------------------------------------

            case PieceType.Knight:
                {
                    int absX = Mathf.Abs(dx);
                    int absY = Mathf.Abs(dy);

                    return
                        (absX == 1 && absY == 2) ||
                        (absX == 2 && absY == 1);
                }

            // -------------------------------------------------
            // BISHOP
            // -------------------------------------------------

            case PieceType.Bishop:
                {
                    if (Mathf.Abs(dx) != Mathf.Abs(dy))
                        return false;

                    return IsPathClear(from, target);
                }

            // -------------------------------------------------
            // ROOK
            // -------------------------------------------------

            case PieceType.Rook:
                {
                    if (dx != 0 && dy != 0)
                        return false;

                    return IsPathClear(from, target);
                }

            // -------------------------------------------------
            // QUEEN
            // -------------------------------------------------

            case PieceType.Queen:
                {
                    bool diagonal =
                        Mathf.Abs(dx) == Mathf.Abs(dy);

                    bool straight =
                        dx == 0 || dy == 0;

                    if (!diagonal && !straight)
                        return false;

                    return IsPathClear(from, target);
                }

            // -------------------------------------------------
            // KING
            //
            // IMPORTANT:
            // Only adjacent attacks.
            // Castling is NOT an attack.
            // -------------------------------------------------

            case PieceType.King:
                {
                    return
                        Mathf.Abs(dx) <= 1 &&
                        Mathf.Abs(dy) <= 1;
                }

            default:
                return false;
        }
    }

    // =========================================================
    // SLIDING PIECE PATH CHECK
    // =========================================================

    private static bool IsPathClear(
        Vector2Int from,
        Vector2Int target)
    {
        int stepX =
            target.x == from.x
                ? 0
                : target.x > from.x
                    ? 1
                    : -1;

        int stepY =
            target.y == from.y
                ? 0
                : target.y > from.y
                    ? 1
                    : -1;

        Vector2Int current =
            new Vector2Int(
                from.x + stepX,
                from.y + stepY
            );

        // Stop BEFORE the target.
        // The target itself may contain the enemy king.
        while (current != target)
        {
            if (ChessBoard.Instance.GetPieceAt(current) != null)
                return false;

            current += new Vector2Int(stepX, stepY);
        }

        return true;
    }
}