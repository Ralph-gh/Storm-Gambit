using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TurnIndicator : MonoBehaviour
{
    // Start is called before the first frame update

    public TextMeshProUGUI turnText;
    private void Start()
    {
        // Subscribe to turn change events
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged += UpdateIndicator;

        // Initial sync
        if (GameState.Instance != null)
            UpdateIndicator(GameState.Instance.CurrentTurn.Value);
        else if (TurnManager.Instance != null)
            UpdateIndicator(TurnManager.Instance.currentTurn);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= UpdateIndicator;
    }

    private void UpdateIndicator(TeamColor side)
    {
        if (turnText == null) return;
        turnText.text = (side == TeamColor.White) ? "White to Play" : "Black to Play";
    }
}
