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
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnManager();
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
        if (isThinking)
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

            string bestMoveText =
                await stockfish.GetBestMoveAsync(fen);

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

            if (!UciMoveParser.TryParse(
                bestMoveText,
                out UciMove move))
            {
                Debug.LogError(
                    $"[AI] Could not parse Stockfish move: {bestMoveText}"
                );
                return;
            }

            ChessPiece piece =
                ChessBoard.Instance.GetPieceAt(move.From);

            if (piece == null)
            {
                Debug.LogError(
                    $"[AI] No piece at {move.From} for {bestMoveText}."
                );
                return;
            }

            if (piece.team != aiSide)
            {
                Debug.LogError(
                    $"[AI] Wrong-side piece selected for {bestMoveText}."
                );
                return;
            }

            if (move.Promotion.HasValue)
            {
                Debug.LogWarning(
                    $"[AI] Promotion received: {bestMoveText}. " +
                    "AI promotion choice will be wired in the promotion pass."
                );
            }

            Debug.Log(
                $"[AI] Executing {piece.team} {piece.pieceType}: " +
                $"{bestMoveText}"
            );

            bool accepted =
                piece.TryExecuteAIMove(move.To);

            if (!accepted)
            {
                Debug.LogWarning(
                    $"[AI] Storm Gambit rejected {bestMoveText}. " +
                    "Later, magic restrictions will be sent to Stockfish " +
                    "through UCI searchmoves."
                );
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
}
