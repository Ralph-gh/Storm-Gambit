using UnityEngine;
using UnityEngine.EventSystems;

public class DivineProtectionSpellUI : MonoBehaviour
{
    private CardUI sourceCard;
    private bool hasClosed;
    private bool awaitingServer;

    [Header("UI")]
    [SerializeField] private GameObject legacyDivineProtectionPanel;

    private SpellPromptPanelUI activePrompt;
    // CardUI calls this via BroadcastMessage("BindSourceCard", this)
    public void BindSourceCard(CardUI card) => sourceCard = card;
    void Start()
    {
        Debug.Log("Divine Protection UI active.");

        // PATCH: hide old UI
        if (legacyDivineProtectionPanel != null)
            legacyDivineProtectionPanel.SetActive(false);

        // PATCH: show new reusable prompt
        if (SpellOverlayManager.Instance != null)
        {
            activePrompt = SpellOverlayManager.Instance.ShowActionPrompt(
                "Select a piece to protect for one turn. " +
                "That piece cannot move until next turn",
                CancelSpell
            );
        }
        else
        {
            Debug.LogWarning(
                "[DIVINE PROTECTION] SpellOverlayManager not found."
            );
        }
    }
    void Update()
    {
        if (awaitingServer || hasClosed) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSpell();
            return;
        }
        TeamColor side = SpellRules.IsNet && NetPlayer.Local != null
            ? NetPlayer.Local.Side.Value : TurnManager.Instance.currentTurn;
        if (!SpellRules.CanCastNow(side)) return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = WorldToCell(mouseWorld);

            if (!ChessBoard.Instance.IsInsideBoard(cell)) return;

            ChessPiece piece = ChessBoard.Instance.GetPieceAt(cell);

            // Only allow selecting your own piece on your turn
            if (piece != null && piece.team == side)
            {
                if (Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    if (GameState.Instance == null) return;
                    awaitingServer = true;
                    GameState.Instance.RequestSpell(NetworkSpellKind.Protection, accepted =>
                    {
                        if (this == null) return;
                        awaitingServer = false;
                        if (accepted) CloseSuccess();
                        else activePrompt?.SetMessage("Protection rejected. Select a valid piece, or cancel.");
                    }, piece.Id);
                    return;
                }
                else
                    piece.ApplyDivineProtectionOneTurn(); // offline
                //TurnManager.Instance.NextTurn();       commented to no longer end turn
                if (TurnManager.Instance.IsPlayersTurn(piece.team))
                    TurnManager.Instance.RegisterFreeSpellCast();
                // If Divine Protection immobilized the last available piece,
                // automatically finish the turn.
                if (!(Unity.Netcode.NetworkManager.Singleton &&
                      Unity.Netcode.NetworkManager.Singleton.IsListening))
                {
                    if (!TurnManager.Instance.HasAnyMovablePiece(
                            TurnManager.Instance.currentTurn))
                    {
                        Debug.Log(
                            $"[TURN] {TurnManager.Instance.currentTurn} has no movable pieces after Divine Protection. Ending turn."
                        );

                        TurnManager.Instance.NextTurn();
                    }
                }

                CloseSuccess(); // consumes the card + closes UI
            }
        }
    }
    public void CancelSpell()
    {
        if (hasClosed || awaitingServer) return;
        hasClosed = true;

        if (activePrompt != null)
        {
            activePrompt.Close();
            activePrompt = null;
        }
        sourceCard?.CancelPendingSpellCast();
        Destroy(gameObject);
    }

    private void CloseSuccess()
    {
        if (hasClosed) return;
        hasClosed = true;

        if (activePrompt != null)
        {
            activePrompt.Close();
            activePrompt = null;
        }
        sourceCard?.ConsumeCardAfterSuccessfulCast();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // If something kills the UI unexpectedly, treat as cancel.
        if (!hasClosed)
        {
            if (activePrompt != null)
            {
                activePrompt.Close();
                activePrompt = null;
            }

            sourceCard?.CancelPendingSpellCast();
        }
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }
}

