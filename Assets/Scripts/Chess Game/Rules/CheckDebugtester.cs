using UnityEngine;

public class CheckDebugTester : MonoBehaviour
{
    [Header("Check Detection Test")]
    [SerializeField]
    private TeamColor teamToTest = TeamColor.White;

    [ContextMenu("Test King Check")]
    public void TestKingCheck()
    {
        ChessPiece king =
            ChessAttackEvaluator.FindKing(teamToTest);

        if (king == null)
        {
            Debug.LogError(
                $"[CHECK TEST] {teamToTest} king not found."
            );

            return;
        }

        TeamColor enemyTeam =
            teamToTest == TeamColor.White
                ? TeamColor.Black
                : TeamColor.White;

        bool inCheck =
            ChessAttackEvaluator.TryGetAttacker(
                king.currentCell,
                enemyTeam,
                out ChessPiece attacker
            );

        if (inCheck)
        {
            Debug.Log(
                $"[CHECK TEST] {teamToTest} KING IS IN CHECK " +
                $"at {king.currentCell}. " +
                $"Attacker = {attacker.team} " +
                $"{attacker.pieceType} " +
                $"at {attacker.currentCell}."
            );
        }
        else
        {
            Debug.Log(
                $"[CHECK TEST] {teamToTest} king is SAFE " +
                $"at {king.currentCell}."
            );
        }
    }
}