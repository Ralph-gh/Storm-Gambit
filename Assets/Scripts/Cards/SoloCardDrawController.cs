using UnityEngine;

public class SoloCardDrawController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CardDrawer playerCardDrawer;

    [Header("Draw Settings")]
    [SerializeField] private int movesPerCardDraw = 10;

    private int fullMoveNumber = 1;
    private TeamColor previousTurn;
    private bool initialized;

    private void Start()
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogError("[SOLO CARDS] TurnManager not found.");
            return;
        }

        previousTurn = TurnManager.Instance.currentTurn;
        initialized = true;

        TurnManager.Instance.OnTurnChanged += HandleTurnChanged;

        Debug.Log("[SOLO CARDS] Solo card drawing initialized.");
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleTurnChanged(TeamColor newTurn)
    {
        if (!initialized)
        {
            previousTurn = newTurn;
            initialized = true;
            return;
        }

        // Black -> White means one complete chess move has finished.
        if (previousTurn == TeamColor.Black &&
            newTurn == TeamColor.White)
        {
            fullMoveNumber++;

            Debug.Log(
                $"[SOLO CARDS] Full move {fullMoveNumber}"
            );

            if (fullMoveNumber > 0 &&
                fullMoveNumber % movesPerCardDraw == 0)
            {
                if (playerCardDrawer != null)
                {
                    playerCardDrawer.DrawOneSpellCard();

                    Debug.Log(
                        "[SOLO CARDS] Player drew a spell card."
                    );
                }
                else
                {
                    Debug.LogError(
                        "[SOLO CARDS] CardDrawer reference missing."
                    );
                }
            }
        }

        previousTurn = newTurn;
    }
}