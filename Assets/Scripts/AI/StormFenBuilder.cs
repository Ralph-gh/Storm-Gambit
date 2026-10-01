using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Converts the CURRENT Storm Gambit board into a standard FEN snapshot.
/// Stockfish is intentionally told only what the classical board looks like.
/// Spell history is not sent.
/// </summary>
public static class StormFenBuilder
{
    public static string Build(TeamColor sideToMove)
    {
        if (ChessBoard.Instance == null)
            throw new InvalidOperationException("ChessBoard.Instance is null.");

        StringBuilder placement = new StringBuilder();

        // FEN is written from rank 8 down to rank 1.
        // Storm Gambit already uses y=0 as rank 1 and y=7 as rank 8.
        for (int y = 7; y >= 0; y--)
        {
            int emptySquares = 0;

            for (int x = 0; x < 8; x++)
            {
                ChessPiece piece =
                    ChessBoard.Instance.GetPieceAt(new Vector2Int(x, y));

                if (piece == null)
                {
                    emptySquares++;
                    continue;
                }

                if (emptySquares > 0)
                {
                    placement.Append(emptySquares);
                    emptySquares = 0;
                }

                placement.Append(ToFenPiece(piece));
            }

            if (emptySquares > 0)
                placement.Append(emptySquares);

            if (y > 0)
                placement.Append('/');
        }

        string activeColor =
            sideToMove == TeamColor.White ? "w" : "b";

        string castlingRights = BuildCastlingRights();

        // PHASE 1:
        // We intentionally do not send en-passant state yet.
        // Once the basic engine loop works, we will wire Storm Gambit's
        // enPassantTarget into this field.
        string enPassant = "-";

        // These counters are not important for our first Stockfish integration.
        // We will later wire them to the real game state if needed.
        int halfmoveClock = 0;
        int fullmoveNumber = 1;

        return $"{placement} {activeColor} {castlingRights} " +
               $"{enPassant} {halfmoveClock} {fullmoveNumber}";
    }

    private static char ToFenPiece(ChessPiece piece)
    {
        char value = piece.pieceType switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new ArgumentOutOfRangeException()
        };

        return piece.team == TeamColor.White
            ? char.ToUpperInvariant(value)
            : value;
    }

    private static string BuildCastlingRights()
    {
        StringBuilder rights = new StringBuilder();

        // White king: e1
        ChessPiece whiteKing =
            ChessBoard.Instance.GetPieceAt(new Vector2Int(4, 0));

        if (IsUnmoved(whiteKing, TeamColor.White, PieceType.King))
        {
            // h1 rook = White kingside
            ChessPiece h1 =
               ChessBoard.Instance.GetPieceAt(new Vector2Int(7, 0));

            if (IsUnmoved(h1, TeamColor.White, PieceType.Rook))
                rights.Append('K');

            // a1 rook = White queenside
            ChessPiece a1 =
                ChessBoard.Instance.GetPieceAt(new Vector2Int(0, 0));

            if (IsUnmoved(a1, TeamColor.White, PieceType.Rook))
                rights.Append('Q');
        }

        // Black king: e8
        ChessPiece blackKing =
            ChessBoard.Instance.GetPieceAt(new Vector2Int(4, 7));

        if (IsUnmoved(blackKing, TeamColor.Black, PieceType.King))
        {
            // h8 rook = Black kingside
            ChessPiece h8 =
                ChessBoard.Instance.GetPieceAt(new Vector2Int(7, 7));

            if (IsUnmoved(h8, TeamColor.Black, PieceType.Rook))
                rights.Append('k');

            // a8 rook = Black queenside
            ChessPiece a8 =
                ChessBoard.Instance.GetPieceAt(new Vector2Int(0, 7));

            if (IsUnmoved(a8, TeamColor.Black, PieceType.Rook))
                rights.Append('q');
        }

        return rights.Length > 0 ? rights.ToString() : "-";
    }

    private static bool IsUnmoved(
        ChessPiece piece,
        TeamColor team,
        PieceType type)
    {
        return piece != null &&
               piece.team == team &&
               piece.pieceType == type &&
               !piece.hasMoved;
    }
}

