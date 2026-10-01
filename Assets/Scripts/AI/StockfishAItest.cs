using System;
using UnityEngine;

/// <summary>
/// Phase-1 integration test.
/// Reads the live Storm Gambit position, asks Stockfish for a move,
/// parses the answer, and logs what piece Stockfish wants to move.
/// It intentionally DOES NOT execute the move yet.
/// </summary>
public class StockfishAITest : MonoBehaviour
{
    [SerializeField]
    private StockfishEngine stockfish;

    [Header("Optional")]
    [SerializeField]
    private bool testAutomaticallyOnStart = false;

    private async void Start()
    {
        if (testAutomaticallyOnStart)
            await RunCurrentPositionTest();
    }

    [ContextMenu("Test Current Position With Stockfish")]
    public async void TestCurrentPositionWithStockfish()
    {
        await RunCurrentPositionTest();
    }

    private async System.Threading.Tasks.Task
        RunCurrentPositionTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[AI TEST] Enter Play Mode first."
            );
            return;
        }

        if (stockfish == null)
        {
            Debug.LogError(
                "[AI TEST] Assign StockfishEngine in the Inspector."
            );
            return;
        }

        if (ChessBoard.Instance == null)
        {
            Debug.LogError(
                "[AI TEST] ChessBoard.Instance is missing."
            );
            return;
        }

        try
        {
            TeamColor sideToMove =
                TurnManager.Instance != null
                    ? TurnManager.Instance.currentTurn
                    : TeamColor.White;

            string fen =
                StormFenBuilder.Build(sideToMove);

            Debug.Log(
                $"[AI TEST] FEN: {fen}"
            );

            string bestMove =
                await stockfish.GetBestMoveAsync(fen);

            Debug.Log(
                $"[AI TEST] Stockfish bestmove: {bestMove}"
            );

            if (bestMove == "(none)" ||
                bestMove == "0000")
            {
                Debug.LogWarning(
                    "[AI TEST] Stockfish reports no legal move."
                );
                return;
            }

            if (!TryParseUciMove(
                bestMove,
                out Vector2Int from,
                out Vector2Int to,
                out PieceType? promotion))
            {
                Debug.LogError(
                    $"[AI TEST] Could not parse move: {bestMove}"
                );
                return;
            }

            ChessPiece piece =
                ChessBoard.Instance.GetPieceAt(from);

            if (piece == null)
            {
                Debug.LogError(
                    $"[AI TEST] Stockfish chose {bestMove}, " +
                    $"but Storm Gambit has no piece at {from}."
                );
                return;
            }

            string promotionText =
                promotion.HasValue
                    ? $" promoting to {promotion.Value}"
                    : "";

            Debug.Log(
                $"[AI TEST] SUCCESS: Stockfish wants " +
                $"{piece.team} {piece.pieceType} " +
                $"{CellToSquare(from)} -> {CellToSquare(to)}" +
                promotionText
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[AI TEST] {ex.GetType().Name}: {ex.Message}\n" +
                ex.StackTrace
            );
        }
    }

    public static bool TryParseUciMove(
        string move,
        out Vector2Int from,
        out Vector2Int to,
        out PieceType? promotion)
    {
        from = default;
        to = default;
        promotion = null;

        if (string.IsNullOrWhiteSpace(move) ||
            (move.Length != 4 && move.Length != 5))
        {
            return false;
        }

        if (!TrySquareToCell(
            move.Substring(0, 2),
                out from))
        {
            return false;
        }

        if (!TrySquareToCell(
            move.Substring(2, 2),
            out to))
        {
            return false;
        }

        if (move.Length == 5)
        {
            promotion =
                char.ToLowerInvariant(move[4]) switch
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

        return true;
    }

    public static bool TrySquareToCell(
        string square,
        out Vector2Int cell)
    {
        cell = default;

        if (square == null || square.Length != 2)
            return false;

        char file =
            char.ToLowerInvariant(square[0]);

        char rank = square[1];

        if (file < 'a' || file > 'h' ||
            rank < '1' || rank > '8')
        {
            return false;
        }

        cell =
            new Vector2Int(
                file - 'a',
                rank - '1'
            );

        return true;
    }

    public static string CellToSquare(
        Vector2Int cell)
    {
        if (cell.x < 0 || cell.x > 7 ||
            cell.y < 0 || cell.y > 7)
        {
            return "??";
        }

        char file = (char)('a' + cell.x);
        char rank = (char)('1' + cell.y);

        return $"{file}{rank}";
    }
}
