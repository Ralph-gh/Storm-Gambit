using UnityEngine;
using UnityEngine.EventSystems;

public class FreezeSpellUI : MonoBehaviour
{
    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local) ? NetPlayer.Local.Side.Value
                                              : TurnManager.Instance.currentTurn;
    private SpellPromptPanelUI activePrompt;
    private bool isMageAbility;

    private System.Action mageAbilitySuccess;
    private System.Action mageAbilityCancel;
    private CardUI sourceCard;
    private bool awaitingServer;
    private bool hasClosed;

    [Header("UI")]
    [SerializeField] private GameObject legacyFreezePanel;

    void Update()
    {
        if (awaitingServer || hasClosed) return;
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSpell();
            return;
        }
        if (!(isMageAbility ? SpellRules.IsTurn(MySide) : SpellRules.CanCastNow(MySide))) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2Int cell = WorldToCell(mouseWorld);

            if (!ChessBoard.Instance.IsInsideBoard(cell)) return;

            ChessPiece piece = ChessBoard.Instance.GetPieceAt(cell);
            if (piece == null) return;

            Freeze(piece);
        }
    }
    void Start()
    {
        if (legacyFreezePanel != null)
            legacyFreezePanel.SetActive(false);

        if (SpellOverlayManager.Instance != null)
        {
            activePrompt = SpellOverlayManager.Instance.ShowActionPrompt(
                "Select a piece to freeze for 2 turns. Piece also becomes immune to capture and destruction",
                CancelSpell
            );
        }
    }
    public void ConfigureAsMageAbility(
    System.Action onSuccess,
    System.Action onCancel)
    {
        isMageAbility = true;
        mageAbilitySuccess = onSuccess;
        mageAbilityCancel = onCancel;
    }
    void Freeze(ChessPiece piece)
    {
        if (Unity.Netcode.NetworkManager.Singleton &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (GameState.Instance == null) return;
            awaitingServer = true;
            GameState.Instance.RequestSpell(NetworkSpellKind.Freeze, accepted =>
            {
                if (this == null) return;
                awaitingServer = false;
                if (accepted) CloseSuccess();
                else activePrompt?.SetMessage("Freeze rejected. Select a valid piece, or cancel.");
            }, piece.Id, mageAbility: isMageAbility);
            return;
        }
        else
        {
            piece.ApplyFreeze(2);

            if (!isMageAbility &&
                TurnManager.Instance.IsPlayersTurn(MySide))
            {
                TurnManager.Instance.RegisterFreeSpellCast();
            }

            TeamColor activeSide = TurnManager.Instance.currentTurn;

            if (!TurnManager.Instance.HasAnyMovablePiece(activeSide))
            {
                Debug.Log(
                    $"[TURN] {activeSide} has no movable pieces after Freeze. Ending turn."
                );

                TurnManager.Instance.NextTurn();
            }
        }

        CloseSuccess();
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

        if (isMageAbility)
        {
            mageAbilitySuccess?.Invoke();
        }
        else
        {
            sourceCard?.ConsumeCardAfterSuccessfulCast();
        }

        Destroy(gameObject);
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

        if (isMageAbility)
        {
            mageAbilityCancel?.Invoke();
        }
        else
        {
            sourceCard?.CancelPendingSpellCast();
        }

        Destroy(gameObject);
    }
    public void BindSourceCard(CardUI card)
    {
        sourceCard = card;
    }
    private void OnDestroy()
    {
        if (hasClosed) return;
        activePrompt?.Close();
        if (isMageAbility) mageAbilityCancel?.Invoke();
        else sourceCard?.CancelPendingSpellCast();
    }
    Vector2Int WorldToCell(Vector3 world)
    {
        const float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size),
                              Mathf.FloorToInt(world.y / size));
    }
}
