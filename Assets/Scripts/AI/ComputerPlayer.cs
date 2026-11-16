using System;
using System.Threading.Tasks;
using UnityEngine;

public class ComputerPlayer : MonoBehaviour
{
    [SerializeField]
    private StockfishEngine stockfish;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float initialDelaySeconds = 0.35f;

    private bool isThinking;
    private bool subscribed;
    
    private async void Start()
    {
        await Task.Yield();

        if (this == null || !isActiveAndEnabled) return;

        SubscribeToTurnManager();

        if (initialDelaySeconds > 0f)
        {
            await Task.Delay(
                Mathf.RoundToInt(initialDelaySeconds * 1000f)
            );
        }

        // Important when the player chose Black:
        // Stockfish is White and must move first.
        TryStartAITurn();
    }

    private void OnEnable()
    {
        SubscribeToTurnManager();
        SpellCastState.CastingChanged += HandleCastingChanged;
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnManager();
        SpellCastState.CastingChanged -= HandleCastingChanged;
    }

    private void HandleCastingChanged(bool casting)
    {
        if (!casting) TryStartAITurn();
    }

    private void SubscribeToTurnManager()
    {
        if (subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        subscribed = true;
    }

    private void UnsubscribeFromTurnManager()
    {
        if (!subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
        subscribed = false;
    }

    private void HandleTurnChanged(TeamColor activeSide)
    {
        if (!SoloSession.IsConfigured ||
            !SoloSession.AIEnabled ||
            activeSide != SoloSession.AISide)
        {
            return;
        }

        TryStartAITurn();
    }

    private void TryStartAITurn()
    {
        if (!CanAIActNow())
            return;

        _ = TakeTurnAsync();
    }

    private bool CanAIActNow()
    {
        if (this == null || isThinking || !isActiveAndEnabled || SpellRules.IsNet || SpellCastState.IsCasting)
            return false;

        if (!SoloSession.IsConfigured ||
            !SoloSession.AIEnabled ||
            SoloSession.Opponent != SoloOpponent.Stockfish)
        {
            return false;
        }

        if (stockfish == null ||
            TurnManager.Instance == null ||
            ChessBoard.Instance == null)
        {
            return false;
        }

        if (ChessBoard.Instance.gameOver)
            return false;

        return
            TurnManager.Instance.currentTurn ==
            SoloSession.AISide;
    }

    private async Task TakeTurnAsync()
    {
        if (!CanAIActNow())
            return;

        isThinking = true;

        try
        {
            TeamColor aiSide = SoloSession.AISide;

            string fen = StormFenBuilder.Build(aiSide);

            Debug.Log(
                $"[AI] {aiSide} thinking from FEN: {fen}"
            );

            string bestMoveText;

            try
            {
                bestMoveText =
                    await stockfish.GetBestMoveAsync(fen);
            }
            catch (TimeoutException)
            {
                Debug.LogWarning(
                    $"[AI] Stockfish timed out for {aiSide}. " +
                    "Passing position to Storm no-move resolver."
                );

                stockfish.StopSearch();

                if (this == null ||
                    TurnManager.Instance == null ||
                    ChessBoard.Instance == null ||
                    ChessBoard.Instance.gameOver)
                {
                    return;
                }

                if (TurnManager.Instance.currentTurn != aiSide)
                {
                    Debug.LogWarning(
                        "[AI] Turn changed during Stockfish timeout. " +
                        "Ignoring resolver."
                    );

                    return;
                }

                StockfishNoMoveResult noMoveResult =
                    StockfishNoMoveResolver.Resolve(aiSide);

                Debug.Log(
                    $"[AI] Storm timeout resolution = {noMoveResult}"
                );

                if (noMoveResult == StockfishNoMoveResult.EnginePositionMismatch)
                    TryFallbackMove(aiSide);

                return;
            }

            if (this == null ||
                TurnManager.Instance == null ||
                ChessBoard.Instance == null ||
                ChessBoard.Instance.gameOver)
            {
                return;
            }

            // Do not apply an answer if the turn changed meanwhile.
            if (TurnManager.Instance.currentTurn != aiSide)
            {
                Debug.LogWarning(
                    $"[AI] Ignoring stale Stockfish move {bestMoveText}."
                );
                return;
            }
            // =========================================================
            // STOCKFISH HAS NO USABLE MOVE
            // =========================================================

            string normalizedBestMove =
                bestMoveText?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedBestMove) ||
                normalizedBestMove == "(none)" ||
                normalizedBestMove == "0000")
            {
                Debug.LogWarning(
                    $"[AI] Stockfish has NO MOVE for {aiSide}. " +
                    "Forcing Storm Gambit resolution."
                );

                StockfishNoMoveResult noMoveResult =
                    StockfishNoMoveResolver.Resolve(aiSide);

                Debug.Log(
                    $"[AI] Forced no-move result = {noMoveResult}"
                );

                if (noMoveResult == StockfishNoMoveResult.EnginePositionMismatch)
                    TryFallbackMove(aiSide);

                return;
            }

            // =========================================================
            // NORMAL STOCKFISH MOVE
            // =========================================================

            if (!UciMoveParser.TryParse(
                    bestMoveText,
                    out UciMove move))
            {
                Debug.LogError(
                    $"[AI] Could not parse Stockfish move: {bestMoveText}"
                );

                TryFallbackMove(aiSide);

                return;
            }

            ChessPiece piece =
                ChessBoard.Instance.GetPieceAt(move.From);

            if (piece == null)
            {
                Debug.LogError(
                    $"[AI] No piece at {move.From} for {bestMoveText}."
                );
                TryFallbackMove(aiSide);
                return;
            }

            if (piece.team != aiSide)
            {
                Debug.LogError(
                    $"[AI] Wrong-side piece selected for {bestMoveText}."
                );
                TryFallbackMove(aiSide);
                return;
            }

            if (move.Promotion.HasValue)
            {
                Debug.Log(
                    $"[AI] Stockfish promotion choice: " +
                    $"{move.Promotion.Value} | UCI={bestMoveText}"
                );
            }

            Debug.Log(
                $"[AI] Executing {piece.team} {piece.pieceType}: " +
                $"{bestMoveText}"
            );

            bool accepted =piece.TryExecuteAIMove(move.To,move.Promotion);

            if (!accepted)
            {
                Debug.LogWarning(
                    $"[AI] Storm Gambit rejected {bestMoveText}; trying a Storm-legal fallback."
                );
                TryFallbackMove(aiSide);
            }
            if (accepted && !ChessBoard.Instance.gameOver)
            {
                TeamColor humanSide = ChessAttackEvaluator.OppositeTeam(aiSide);
                StormEndgameEvaluator.TryResolveCheckmate(humanSide);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[AI] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            );
        }
        finally
        {
            isThinking = false;
        }
    }

    private bool TryFallbackMove(TeamColor aiSide)
    {
        if (this == null || !isActiveAndEnabled || SpellRules.IsNet ||
            ChessBoard.Instance == null || ChessBoard.Instance.gameOver ||
            TurnManager.Instance == null || TurnManager.Instance.currentTurn != aiSide)
            return false;

        StockfishNoMoveResult resolution = StockfishNoMoveResolver.Resolve(aiSide);
        if (resolution != StockfishNoMoveResult.EnginePositionMismatch) return true;

        if (!StockfishNoMoveResolver.TryGetValidEscape(aiSide,
                out ChessPiece piece, out Vector2Int destination)) return false;
        PieceType? promotion = piece.pieceType == PieceType.Pawn &&
            Pawn.ShouldPromote(destination, aiSide) ? PieceType.Queen : (PieceType?)null;
        bool accepted = piece.TryExecuteAIMove(destination, promotion);
        if (accepted && !ChessBoard.Instance.gameOver)
            StormEndgameEvaluator.TryResolveCheckmate(ChessAttackEvaluator.OppositeTeam(aiSide));
        return accepted;
    }
}
