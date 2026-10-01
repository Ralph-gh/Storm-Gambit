using UnityEngine;

public readonly struct UciMove
{
    public readonly Vector2Int From;
    public readonly Vector2Int To;
    public readonly PieceType? Promotion;

    public UciMove(
        Vector2Int from,
        Vector2Int to,
        PieceType? promotion)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }
}

public static class UciMoveParser
{
    public static bool TryParse(
        string text,
        out UciMove move)
    {
        move = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        if (text == "0000" || text == "(none)")
            return false;

        if (text.Length != 4 && text.Length != 5)
            return false;

        if (!TrySquareToCell(text.Substring(0, 2), out Vector2Int from))
            return false;

        if (!TrySquareToCell(text.Substring(2, 2), out Vector2Int to))
            return false;

        PieceType? promotion = null;

        if (text.Length == 5)
        {
            promotion = char.ToLowerInvariant(text[4]) switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => null
            };

            if (!promotion.HasValue)
                return false;
        }

        move = new UciMove(from, to, promotion);
        return true;
    }

    public static bool TrySquareToCell(
        string square,
        out Vector2Int cell)
    {
        cell = default;

        if (square == null || square.Length != 2)
            return false;

        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];

        if (file < 'a' || file > 'h' ||
            rank < '1' || rank > '8')
        {
            return false;
        }

        cell = new Vector2Int(
            file - 'a',
            rank - '1'
        );

        return true;
    }
}
