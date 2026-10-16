using UnityEngine;

public class StockfishNoMoveDebugTester : MonoBehaviour
{
    [Header("No-Move Resolver Test")]
    [SerializeField]
    private TeamColor aiTeam = TeamColor.Black;

    [ContextMenu("Test Stockfish No-Move Resolver")]
    public void TestNoMoveResolver()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[NO MOVE TEST] Enter Play Mode first."
            );

            return;
        }

        if (ChessBoard.Instance == null)
        {
            Debug.LogError(
                "[NO MOVE TEST] ChessBoard.Instance missing."
            );

            return;
        }

        if (TurnManager.Instance == null)
        {
            Debug.LogError(
                "[NO MOVE TEST] TurnManager.Instance missing."
            );

            return;
        }

        Debug.Log(
            $"[NO MOVE TEST] Starting resolver. " +
            $"AI={aiTeam}, CurrentTurn=" +
            $"{TurnManager.Instance.currentTurn}"
        );

        StockfishNoMoveResult result =
            StockfishNoMoveResolver.Resolve(aiTeam);

        Debug.Log(
            $"[NO MOVE TEST] RESULT = {result}"
        );
    }
}