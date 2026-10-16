using UnityEngine;

public enum ChessMoveInvalidReason
{
    None,

    NullPiece,
    BoardMissing,
    DestinationOutsideBoard,

    FriendlyPieceOnDestination,

    PieceMovementRuleViolation,

    KingCurrentlyInCheckWhileCastling,
    KingPassesThroughCheckWhileCastling,

    LeavesKingInCheck
}

public struct ChessMoveValidationResult
{
    public bool IsLegal;
    public ChessMoveInvalidReason Reason;
    public string Message;

    public static ChessMoveValidationResult Legal()
    {
        return new ChessMoveValidationResult
        {
            IsLegal = true,
            Reason = ChessMoveInvalidReason.None,
            Message = "Legal move."
        };
    }

    public static ChessMoveValidationResult Illegal(
        ChessMoveInvalidReason reason,
        string message)
    {
        return new ChessMoveValidationResult
        {
            IsLegal = false,
            Reason = reason,
            Message = message
        };
    }
}

public static class ChessMoveValidator
{
    // =========================================================
    // SIMPLE PUBLIC API
    // =========================================================

    public static bool IsLegalChessMove(
        ChessPiece piece,
        Vector2Int destination)
    {
        return Validate(
            piece,
            destination
        ).IsLegal;
    }

    // =========================================================
    // FULL VALIDATION
    //
    // Use this version when we want to know WHY a move failed.
    // Useful for:
    // - debugging
    // - UI messages later
    // - AI diagnostics
    // - checkmate search
    // =========================================================

    public static ChessMoveValidationResult Validate(
        ChessPiece piece,
        Vector2Int destination)
    {
        // -----------------------------------------------------
        // BASIC SAFETY
        // -----------------------------------------------------

        if (piece == null)
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.NullPiece,
                "No chess piece was supplied."
            );
        }

        if (ChessBoard.Instance == null)
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.BoardMissing,
                "ChessBoard.Instance is missing."
            );
        }

        if (!ChessBoard.Instance.IsInsideBoard(destination))
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.DestinationOutsideBoard,
                $"Destination {destination} is outside the board."
            );
        }

        // -----------------------------------------------------
        // CANNOT LAND ON YOUR OWN PIECE
        // -----------------------------------------------------

        ChessPiece destinationPiece =
            ChessBoard.Instance.GetPieceAt(destination);

        if (destinationPiece != null &&
            destinationPiece.team == piece.team)
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.FriendlyPieceOnDestination,
                $"Destination {destination} contains a friendly piece."
            );
        }

        // -----------------------------------------------------
        // EXISTING MOVEMENT RULES
        //
        // We deliberately reuse the game's currently-working
        // Pawn / Knight / Bishop / Rook / Queen / King rules.
        // -----------------------------------------------------

        if (!ChessBoard.Instance.IsLegalMove(
                piece,
                destination))
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.PieceMovementRuleViolation,
                $"{piece.pieceType} cannot move from " +
                $"{piece.currentCell} to {destination}."
            );
        }

        // -----------------------------------------------------
        // CASTLING REQUIRES EXTRA KING-SAFETY CHECKS
        // -----------------------------------------------------

        if (IsCastlingMove(piece, destination))
        {
            ChessMoveValidationResult castleResult =
                ValidateCastlingSafety(
                    piece,
                    destination
                );

            if (!castleResult.IsLegal)
                return castleResult;
        }

        // -----------------------------------------------------
        // FINAL POSITION MUST LEAVE OWN KING SAFE
        // -----------------------------------------------------

        bool leavesKingInCheck =
            ChessPositionSimulator
                .WouldLeaveKingInCheck(
                    piece,
                    destination
                );

        if (leavesKingInCheck)
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason.LeavesKingInCheck,
                $"Move {piece.currentCell} -> {destination} " +
                $"would leave the {piece.team} king in check."
            );
        }

        return ChessMoveValidationResult.Legal();
    }

    // =========================================================
    // CASTLING
    // =========================================================

    private static bool IsCastlingMove(
        ChessPiece piece,
        Vector2Int destination)
    {
        if (piece.pieceType != PieceType.King)
            return false;

        return
            destination.y == piece.currentCell.y &&
            Mathf.Abs(
                destination.x -
                piece.currentCell.x
            ) == 2;
    }

    private static ChessMoveValidationResult
        ValidateCastlingSafety(
            ChessPiece king,
            Vector2Int destination)
    {
        // -----------------------------------------------------
        // RULE 1:
        // Cannot castle while already in check.
        // -----------------------------------------------------

        if (ChessAttackEvaluator.IsKingInCheck(
                king.team))
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason
                    .KingCurrentlyInCheckWhileCastling,

                $"{king.team} cannot castle while in check."
            );
        }

        // -----------------------------------------------------
        // RULE 2:
        // King cannot cross an attacked square.
        //
        // Example:
        //
        // e1 -> g1
        // king passes through f1.
        // -----------------------------------------------------

        int direction =
            destination.x > king.currentCell.x
                ? 1
                : -1;

        Vector2Int transitSquare =
            new Vector2Int(
                king.currentCell.x + direction,
                king.currentCell.y
            );

        bool transitIsUnsafe =
            ChessPositionSimulator
                .WouldLeaveKingInCheck(
                    king,
                    transitSquare
                );

        if (transitIsUnsafe)
        {
            return ChessMoveValidationResult.Illegal(
                ChessMoveInvalidReason
                    .KingPassesThroughCheckWhileCastling,

                $"King cannot castle through attacked " +
                $"square {transitSquare}."
            );
        }

        // Final destination safety is checked by the normal
        // WouldLeaveKingInCheck() call after this method.

        return ChessMoveValidationResult.Legal();
    }
}