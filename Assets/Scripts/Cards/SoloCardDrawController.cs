using UnityEngine;

public class SoloCardDrawController : MonoBehaviour
{
    [SerializeField] private CardDrawer playerCardDrawer;
    private readonly CardDrawSchedule schedule = new();

    void Start() => TurnManager.Instance.ChessMoveCompleted += HandleMove;
    void OnDestroy()
    {
        if (TurnManager.Instance) TurnManager.Instance.ChessMoveCompleted -= HandleMove;
    }

    private void HandleMove(TeamColor side)
    {
        if (!SpellRules.IsNet && ChessBoard.Instance && !ChessBoard.Instance.gameOver &&
            schedule.Record(side) && playerCardDrawer)
            playerCardDrawer.DrawOneSpellCard();
    }
}

public sealed class CardDrawSchedule
{
    private int whiteMoves;
    private int blackMoves;
    private int nextDraw = 7;

    public bool Record(TeamColor side)
    {
        if (side == TeamColor.White) whiteMoves++;
        else blackMoves++;
        if (System.Math.Min(whiteMoves, blackMoves) < nextDraw) return false;
        nextDraw += 7;
        return true;
    }
}
