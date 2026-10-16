using UnityEngine;

public static class ChessPositionSimulator
{
    // =========================================================
    // PUBLIC API
    // =========================================================

    public static bool WouldLeaveKingInCheck(
        ChessPiece piece,
        Vector2Int destination)
    {
        if (piece == null)
        {
            Debug.LogWarning(
                "[SIMULATION] Cannot simulate a null piece."
            );

            return true;
        }

        if (ChessBoard.Instance == null)
        {
            Debug.LogWarning(
                "[SIMULATION] ChessBoard.Instance is null."
            );

            return true;
        }

        if (!ChessBoard.Instance.IsInsideBoard(destination))
        {
            return true;
        }

        ChessPiece[,] simulatedBoard =
            CreateBoardCopy();

        Vector2Int from =
            piece.currentCell;

        // Sanity check.
        if (simulatedBoard[from.x, from.y] != piece)
        {
            Debug.LogWarning(
                $"[SIMULATION] Board mismatch for " +
                $"{piece.team} {piece.pieceType} " +
                $"at {from}."
            );

            return true;
        }

        ApplyMoveToSimulation(
            simulatedBoard,
            piece,
            from,
            destination
        );

        bool kingInCheck =
            ChessAttackEvaluator.IsKingInCheck(
                piece.team,
                cell =>
                    GetPieceAt(
                        simulatedBoard,
                        cell
                    )
            );

        return kingInCheck;
    }

    // =========================================================
    // COPY CURRENT BOARD
    // =========================================================

    private static ChessPiece[,] CreateBoardCopy()
    {
        ChessPiece[,] copy =
            new ChessPiece[8, 8];

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);

                copy[x, y] =
                    ChessBoard.Instance.GetPieceAt(cell);
            }
        }

        return copy;
    }

    // =========================================================
    // SIMULATE MOVE
    // =========================================================

    private static void ApplyMoveToSimulation(
        ChessPiece[,] board,
        ChessPiece piece,
        Vector2Int from,
        Vector2Int destination)
    {
        // -----------------------------------------------------
        // EN PASSANT
        //
        // Destination is empty, but another pawn disappears.
        // This matters because removing that pawn may expose
        // a rook / bishop / queen attack on the king.
        // -----------------------------------------------------

        if (IsEnPassantSimulation(
                board,
                piece,
                from,
                destination))
        {
            Vector2Int capturedPawnCell =
                new Vector2Int(
                    destination.x,
                    from.y
                );

            board[
                capturedPawnCell.x,
                capturedPawnCell.y
            ] = null;
        }

        // -----------------------------------------------------
        // NORMAL MOVE / NORMAL CAPTURE
        //
        // Assigning the mover to destination automatically
        // replaces any captured piece in our temporary board.
        // -----------------------------------------------------

        board[from.x, from.y] = null;

        board[
            destination.x,
            destination.y
        ] = piece;

        // -----------------------------------------------------
        // CASTLING
        //
        // Move the rook in the simulated final position too.
        //
        // IMPORTANT:
        // This only simulates the final position.
        // The later MoveValidator will separately check:
        // - king is not currently in check
        // - transit square is not attacked
        // - destination is not attacked
        // -----------------------------------------------------

        if (piece.pieceType == PieceType.King &&
            Mathf.Abs(destination.x - from.x) == 2 &&
            destination.y == from.y)
        {
            bool kingSide =
                destination.x > from.x;

            Vector2Int rookFrom =
                new Vector2Int(
                    kingSide ? 7 : 0,
                    from.y
                );

            Vector2Int rookTo =
                new Vector2Int(
                    kingSide
                        ? destination.x - 1
                        : destination.x + 1,
                    from.y
                );

            ChessPiece rook =
                GetPieceAt(board, rookFrom);

            if (rook != null &&
                rook.team == piece.team &&
                rook.pieceType == PieceType.Rook)
            {
                board[
                    rookFrom.x,
                    rookFrom.y
                ] = null;

                board[
                    rookTo.x,
                    rookTo.y
                ] = rook;
            }
        }
    }

    // =========================================================
    // EN PASSANT DETECTION
    // =========================================================

    private static bool IsEnPassantSimulation(
        ChessPiece[,] board,
        ChessPiece piece,
        Vector2Int from,
        Vector2Int destination)
    {
        if (piece.pieceType != PieceType.Pawn)
            return false;

        if (ChessBoard.Instance.enPassantTarget.x < 0)
            return false;

        if (destination !=
            ChessBoard.Instance.enPassantTarget)
        {
            return false;
        }

        // En passant destination must be empty.
        if (GetPieceAt(board, destination) != null)
            return false;

        int direction =
            piece.team == TeamColor.White
                ? 1
                : -1;

        bool correctMovement =
            Mathf.Abs(destination.x - from.x) == 1 &&
            destination.y - from.y == direction;

        if (!correctMovement)
            return false;

        Vector2Int victimCell =
            new Vector2Int(
                destination.x,
                from.y
            );

        ChessPiece victim =
            GetPieceAt(board, victimCell);

        return
            victim != null &&
            victim.pieceType == PieceType.Pawn &&
            victim.team != piece.team;
    }

    // =========================================================
    // SAFE ARRAY LOOKUP
    // =========================================================

    private static ChessPiece GetPieceAt(
        ChessPiece[,] board,
        Vector2Int cell)
    {
        if (cell.x < 0 ||
            cell.x >= 8 ||
            cell.y < 0 ||
            cell.y >= 8)
        {
            return null;
        }

        return board[cell.x, cell.y];
    }
}