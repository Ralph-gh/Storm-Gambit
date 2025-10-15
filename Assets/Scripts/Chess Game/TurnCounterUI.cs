using UnityEngine;
using TMPro;
using System;

public class TurnCounterUI : MonoBehaviour
{
    public static event Action<int> OnMoveNumberChanged; // broadcast bus
    [SerializeField] private TextMeshProUGUI moveLabel;  // assign in Inspector

    private void OnEnable()
    {
        OnMoveNumberChanged += HandleMoveChanged;
    }
    private void OnDisable()
    {
        OnMoveNumberChanged -= HandleMoveChanged;
    }

    private void HandleMoveChanged(int moveNumber)
    {
        if (moveLabel != null)
            moveLabel.text = (moveNumber <= 0) ? "Move: —" : $"Move: {moveNumber}";
    }

    // Called by net/offline managers
    public static void BroadcastMoveNumber(int moveNumber)
    {
        OnMoveNumberChanged?.Invoke(moveNumber);
    }
}
