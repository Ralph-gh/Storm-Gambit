using UnityEngine;

public enum StockfishNoMoveResult
{
    None,

    HumanKingCaptured,
    StockfishCheckmated,
    Stalemate,

    // Stockfish said "no move", but according to our
    // Storm-aware board logic the AI still has an escape.
    EnginePositionMismatch
}

public static class StockfishNoMoveResolver
{
    // =========================================================
    // MAIN ENTRY POINT
    // =========================================================

    public static StockfishNoMoveResult Resolve(
        TeamColor aiTeam)
    {
        if (ChessBoard.Instance == null)
        {
            Debug.LogError(
                "[NO MOVE] ChessBoard.Instance is missing."
            );

            return StockfishNoMoveResult.None;
        }

        if (ChessBoard.Instance.gameOver)
            return StockfishNoMoveResult.None;

        TeamColor humanTeam =
            ChessAttackEvaluator.OppositeTeam(aiTeam);

        Debug.Log(
            $"[NO MOVE] Resolving Stockfish no-move state. " +
            $"AI={aiTeam}, Human={humanTeam}"
        );

        // =====================================================
        // PRIORITY 1
        // HUMAN LEFT KING CAPTURABLE
        //
        // This is Storm Gambit behavior.
        // Do NOT use classical king-safety rules here.
        // =====================================================

        if (TryCaptureHumanKing(
                aiTeam,
                humanTeam))
        {
            Debug.Log(
                "[NO MOVE] Human king was capturable. " +
                "Forced king capture executed."
            );

            return StockfishNoMoveResult.HumanKingCaptured;
        }

        // =====================================================
        // PRIORITY 2
        // CHECK STOCKFISH KING
        // =====================================================

        bool aiKingInCheck =
            ChessAttackEvaluator.IsKingInCheck(
                aiTeam
            );

        // =====================================================
        // PRIORITY 3
        // DOES AI HAVE ANY REAL ESCAPE?
        // =====================================================

        bool aiHasEscape =
            HasAnyValidEscape(aiTeam);

        // =====================================================
        // CHECKMATE
        // =====================================================

        if (aiKingInCheck &&
            !aiHasEscape)
        {
            Debug.Log(
                $"[NO MOVE] {aiTeam} is CHECKMATED."
            );

            DeclareHumanVictory(
                humanTeam,
                "Checkmate"
            );

            return
                StockfishNoMoveResult.StockfishCheckmated;
        }

        // =====================================================
        // STALEMATE
        // =====================================================

        if (!aiKingInCheck &&
            !aiHasEscape)
        {
            Debug.Log(
                $"[NO MOVE] {aiTeam} has no move " +
                "and is not in check. STALEMATE."
            );

            DeclareDraw("Stalemate");

            return StockfishNoMoveResult.Stalemate;
        }

        // =====================================================
        // SPECIAL STORM POSITION
        //
        // Stockfish could not interpret the position,
        // but Storm's board rules say the AI still has
        // at least one valid classical escape.
        // =====================================================

        Debug.LogWarning(
            "[NO MOVE] Stockfish returned no move, " +
            "but Storm Gambit found at least one valid AI move. " +
            "Treating this as an engine-position mismatch."
        );

        return
            StockfishNoMoveResult.EnginePositionMismatch;
    }

    // =========================================================
    // FORCE HUMAN KING CAPTURE
    // =========================================================

    private static bool TryCaptureHumanKing(
        TeamColor aiTeam,
        TeamColor humanTeam)
    {
        ChessPiece humanKing =
            ChessAttackEvaluator.FindKing(
                humanTeam
            );

        if (humanKing == null)
        {
            Debug.LogWarning(
                $"[NO MOVE] Could not find {humanTeam} king."
            );

            return false;
        }

        // Existing Storm capture rules currently treat
        // Divine Protection / Freeze as non-capturable.
        if (humanKing.IsDivinelyProtected ||
            humanKing.IsFrozen)
        {
            Debug.Log(
                $"[NO MOVE] {humanTeam} king is protected. " +
                "Forced capture blocked."
            );

            return false;
        }

        Vector2Int kingSquare =
            humanKing.currentCell;

        // Search the full board for an AI attacker.
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece attacker =
                    ChessBoard.Instance.GetPieceAt(
                        new Vector2Int(x, y)
                    );

                if (attacker == null)
                    continue;

                if (attacker.team != aiTeam)
                    continue;

                // These pieces cannot act under current
                // Storm Gambit movement rules.
                if (attacker.IsFrozen ||
                    attacker.IsStunned ||
                    attacker.IsDivinelyProtected)
                {
                    continue;
                }

                // IMPORTANT:
                // Use the existing Storm pseudo-legal move rule.
                //
                // Do NOT use ChessMoveValidator here because we
                // intentionally allow king capture even from a
                // position classical chess considers illegal.
                if (!ChessBoard.Instance.IsLegalMove(
                        attacker,
                        kingSquare))
                {
                    continue;
                }

                Debug.Log(
                    $"[NO MOVE] Forced king capture found: " +
                    $"{aiTeam} {attacker.pieceType} " +
                    $"{attacker.currentCell} -> {kingSquare}"
                );

                PieceType? promotion = null;

                // Rare case:
                // pawn captures the king on its promotion rank.
                if (attacker.pieceType ==
                        PieceType.Pawn &&
                    Pawn.ShouldPromote(
                        kingSquare,
                        attacker.team))
                {
                    promotion = PieceType.Queen;
                }

                bool executed =
                    attacker.TryExecuteAIMove(
                        kingSquare,
                        promotion
                    );

                if (executed)
                {
                    // Ensure Solo victory UI appears even if
                    // CapturePiece only handled gameOver itself.
                    if (ChessBoard.Instance.gameOver)
                    {
                        DeclareHumanLossByCapture(
                            aiTeam
                        );
                    }

                    return true;
                }

                Debug.LogWarning(
                    "[NO MOVE] Forced king capture was found " +
                    "but TryExecuteAIMove rejected it."
                );
            }
        }

        return false;
    }

    // =========================================================
    // DOES STOCKFISH SIDE HAVE ANY VALID ESCAPE?
    //
    // THIS is where classical chess legality is useful.
    // Human movement is NOT restricted by this.
    // =========================================================

    public static bool HasAnyValidEscape(
        TeamColor aiTeam)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece =
                    ChessBoard.Instance.GetPieceAt(
                        new Vector2Int(x, y)
                    );

                if (piece == null ||
                    piece.team != aiTeam)
                {
                    continue;
                }

                // Human spells may remove this AI piece's
                // ability to respond.
                if (piece.IsFrozen ||
                    piece.IsStunned ||
                    piece.IsDivinelyProtected)
                {
                    continue;
                }

                for (int targetX = 0;
                     targetX < 8;
                     targetX++)
                {
                    for (int targetY = 0;
                         targetY < 8;
                         targetY++)
                    {
                        Vector2Int destination =
                            new Vector2Int(
                                targetX,
                                targetY
                            );

                        if (destination ==
                            piece.currentCell)
                        {
                            continue;
                        }

                        ChessPiece target =
                            ChessBoard.Instance.GetPieceAt(
                                destination
                            );

                        // Friendly occupancy.
                        if (target != null &&
                            target.team == aiTeam)
                        {
                            continue;
                        }

                        // Current Storm capture protection.
                        if (target != null &&
                            target.team != aiTeam &&
                            (target.IsDivinelyProtected ||
                             target.IsFrozen))
                        {
                            continue;
                        }

                        ChessMoveValidationResult result =
                            ChessMoveValidator.Validate(
                                piece,
                                destination
                            );

                        if (result.IsLegal)
                        {
                            Debug.Log(
                                $"[NO MOVE] AI escape exists: " +
                                $"{piece.pieceType} " +
                                $"{piece.currentCell} -> " +
                                $"{destination}"
                            );

                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // =========================================================
    // SOLO RESULT HELPERS
    // =========================================================

    private static void DeclareHumanVictory(
        TeamColor humanTeam,
        string reason)
    {
        if (ChessBoard.Instance == null)
            return;

        ChessBoard.Instance.gameOver = true;

        string winnerText =
            humanTeam == TeamColor.White
                ? "White Wins!"
                : "Black Wins!";

        Debug.Log(
            $"Game Over — {winnerText} ({reason})"
        );

        ShowSoloResult(winnerText);
    }

    private static void DeclareHumanLossByCapture(
        TeamColor aiTeam)
    {
        string winnerText =
            aiTeam == TeamColor.White
                ? "White Wins!"
                : "Black Wins!";

        ShowSoloResult(winnerText);
    }

    private static void DeclareDraw(
        string reason)
    {
        if (ChessBoard.Instance == null)
            return;

        ChessBoard.Instance.gameOver = true;

        Debug.Log(
            $"Game Over — Draw ({reason})"
        );

        ShowSoloResult("Draw!");
    }

    private static void ShowSoloResult(
        string message)
    {
        ChessBoard board =
            ChessBoard.Instance;

        if (board == null)
            return;

        if (board.victoryScreen != null)
            board.victoryScreen.SetActive(true);

        if (board.victoryText != null)
            board.victoryText.text = message;

        if (board.audioSource != null &&
            board.victoryClip != null)
        {
            board.audioSource.PlayOneShot(
                board.victoryClip
            );
        }
    }
}