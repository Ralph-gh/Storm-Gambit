using System;
using UnityEngine;

public static class ChessAttackEvaluator
{
    // =========================================================
    // LIVE BOARD WRAPPERS
    // =========================================================

    public static ChessPiece FindKing(TeamColor team)
    {
        if (ChessBoard.Instance == null)
        {
            Debug.LogWarning("[CHECK] ChessBoard.Instance is null.");
            return null;
        }

        return FindKing(
            team,
            ChessBoard.Instance.GetPieceAt
        );
    }

    public static bool IsKingInCheck(TeamColor team)
    {
        if (ChessBoard.Instance == null)
            return false;

        return IsKingInCheck(
            team,
            ChessBoard.Instance.GetPieceAt
        );
    }

    public static bool IsSquareAttacked(
        Vector2Int square,
        TeamColor byTeam)
    {
        if (ChessBoard.Instance == null)
            return false;

        return IsSquareAttacked(
            square,
            byTeam,
            ChessBoard.Instance.GetPieceAt
        );
    }

    public static bool TryGetAttacker(
        Vector2Int square,
        TeamColor byTeam,
        out ChessPiece attacker)
    {
        if (ChessBoard.Instance == null)
        {
            attacker = null;
            return false;
        }

        return TryGetAttacker(
            square,
            byTeam,
            ChessBoard.Instance.GetPieceAt,
            out attacker
        );
    }

    // =========================================================
    // GENERIC BOARD VIEW
    //
    // These versions work with:
    // - the real ChessBoard
    // - simulated board copies
    // - future replay / AI board states
    // =========================================================

    public static ChessPiece FindKing(
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        if (getPieceAt == null)
            return null;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece =
                    getPieceAt(new Vector2Int(x, y));

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

    public static bool IsKingInCheck(
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        Vector2Int kingSquare;

        if (!TryFindKingSquare(
                team,
                getPieceAt,
                out kingSquare))
        {
            Debug.LogWarning(
                $"[CHECK] {team} king was not found."
            );

            return false;
        }

        TeamColor enemyTeam =
            OppositeTeam(team);

        return IsSquareAttacked(
            kingSquare,
            enemyTeam,
            getPieceAt
        );
    }

    public static bool IsSquareAttacked(
        Vector2Int square,
        TeamColor byTeam,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        return TryGetAttacker(
            square,
            byTeam,
            getPieceAt,
            out _
        );
    }

    public static bool TryGetAttacker(
        Vector2Int square,
        TeamColor byTeam,
        Func<Vector2Int, ChessPiece> getPieceAt,
        out ChessPiece attacker)
    {
        attacker = null;

        if (getPieceAt == null)
            return false;

        if (!IsInsideBoard(square))
            return false;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Vector2Int from =
                    new Vector2Int(x, y);

                ChessPiece piece =
                    getPieceAt(from);

                if (piece == null)
                    continue;

                if (piece.team != byTeam)
                    continue;

                if (CanPieceAttackSquare(
                        piece,
                        from,
                        square,
                        getPieceAt))
                {
                    attacker = piece;
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    // RAW PIECE ATTACKS
    // =========================================================

    public static bool CanPieceAttackSquare(
        ChessPiece piece,
        Vector2Int target)
    {
        if (piece == null ||
            ChessBoard.Instance == null)
        {
            return false;
        }

        return CanPieceAttackSquare(
            piece,
            piece.currentCell,
            target,
            ChessBoard.Instance.GetPieceAt
        );
    }

    public static bool CanPieceAttackSquare(
        ChessPiece piece,
        Vector2Int from,
        Vector2Int target,
        Func<Vector2Int, ChessPiece> getPieceAt)
    {
        if (piece == null ||
            getPieceAt == null)
        {
            return false;
        }

        if (!IsInsideBoard(from) ||
            !IsInsideBoard(target))
        {
            return false;
        }

        if (from == target)
            return false;

        int dx = target.x - from.x;
        int dy = target.y - from.y;

        switch (piece.pieceType)
        {
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

            case PieceType.Knight:
                {
                    int absX = Mathf.Abs(dx);
                    int absY = Mathf.Abs(dy);

                    return
                        (absX == 1 && absY == 2) ||
                        (absX == 2 && absY == 1);
                }

            case PieceType.Bishop:
                {
                    if (Mathf.Abs(dx) != Mathf.Abs(dy))
                        return false;

                    return IsPathClear(
                        from,
                        target,
                        getPieceAt
                    );
                }

            case PieceType.Rook:
                {
                    if (dx != 0 && dy != 0)
                        return false;

                    return IsPathClear(
                        from,
                        target,
                        getPieceAt
                    );
                }

            case PieceType.Queen:
                {
                    bool diagonal =
                        Mathf.Abs(dx) == Mathf.Abs(dy);

                    bool straight =
                        dx == 0 || dy == 0;

                    if (!diagonal && !straight)
                        return false;

                    return IsPathClear(
                        from,
                        target,
                        getPieceAt
                    );
                }

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
    // HELPERS
    // =========================================================

    private static bool TryFindKingSquare(
        TeamColor team,
        Func<Vector2Int, ChessPiece> getPieceAt,
        out Vector2Int kingSquare)
    {
        kingSquare = new Vector2Int(-1, -1);

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);

                ChessPiece piece =
                    getPieceAt(cell);

                if (piece == null)
                    continue;

                if (piece.team == team &&
                    piece.pieceType == PieceType.King)
                {
                    kingSquare = cell;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPathClear(
        Vector2Int from,
        Vector2Int target,
        Func<Vector2Int, ChessPiece> getPieceAt)
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

        while (current != target)
        {
            if (getPieceAt(current) != null)
                return false;

            current +=
                new Vector2Int(stepX, stepY);
        }

        return true;
    }

    private static bool IsInsideBoard(
        Vector2Int cell)
    {
        return
            cell.x >= 0 &&
            cell.x < 8 &&
            cell.y >= 0 &&
            cell.y < 8;
    }

    public static TeamColor OppositeTeam(
        TeamColor team)
    {
        return
            team == TeamColor.White
                ? TeamColor.Black
                : TeamColor.White;
    }
}