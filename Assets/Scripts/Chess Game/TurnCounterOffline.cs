using UnityEngine;

public class TurnCounterOffline : MonoBehaviour
{
    [SerializeField] private int moveNumber = 0; // starts at 0
    private TeamColor _lastTurn;

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            _lastTurn = TurnManager.Instance.currentTurn;
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleTurnChanged(TeamColor newTurn)
    {
        // White -> Black: White just moved. If first time, set to 1.
        if (_lastTurn == TeamColor.White && newTurn == TeamColor.Black)
        {
            if (moveNumber == 0) moveNumber = 1;
            TurnCounterUI.BroadcastMoveNumber(moveNumber);
        }
        // Black -> White: Black just moved. Increment full-move count.
        else if (_lastTurn == TeamColor.Black && newTurn == TeamColor.White)
        {
            if (moveNumber > 0) moveNumber += 1;
            TurnCounterUI.BroadcastMoveNumber(moveNumber);

            if (moveNumber > 0 && moveNumber % 10 == 0)
            {
                // Both players draw 1 spell (client-local)
                var drawers = FindObjectsByType<CardDrawer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var d in drawers) d.DrawOneSpellCard();
            }
        }

        _lastTurn = newTurn;
    }
}
