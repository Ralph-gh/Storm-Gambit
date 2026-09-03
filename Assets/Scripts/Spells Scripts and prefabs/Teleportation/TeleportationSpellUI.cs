using UnityEngine;
using UnityEngine.EventSystems;

public class TeleportationSpellUI : MonoBehaviour
{
    private ChessPiece selectedPiece = null;
    private CardUI sourceCard;
    private bool hasClosed;
    private SpellPromptPanelUI activePrompt;
    private bool isMageAbility = false;

    private System.Action mageAbilitySuccess;
    private System.Action mageAbilityCancel;

    // ==========================
    // PATCH - OLD TELEPORT UI
    // ==========================
    [Header("UI")]
    [SerializeField] private GameObject legacyTeleportPanel;
    public void BindSourceCard(CardUI card) => sourceCard = card;
    // Resolve my side for net/offline once per frame
    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local) ? NetPlayer.Local.Side.Value
                                              : TurnManager.Instance.currentTurn;

    void Start()
    {
        Debug.Log("Teleportation Spell UI instantiated and active.");

        // ==========================================
        // PATCH: NEVER SHOW THE OLD TELEPORT PANEL
        // ==========================================
        if (legacyTeleportPanel != null)
            legacyTeleportPanel.SetActive(false);

        // ==========================================
        // NEW REUSABLE SPELL UI
        // ==========================================
        if (SpellOverlayManager.Instance != null)
        {
            activePrompt = SpellOverlayManager.Instance.ShowActionPrompt(
                "Select a piece to teleport.Once teleported that piece has to wait until next turn to move",
                CancelSpell
            );
        }
        else
        {
            Debug.LogWarning("[TELEPORT] SpellOverlayManager not found.");
        }
    }

    void Update()
    {
        // Cancel with right-click or Esc
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSpell();
            return;
        }

        // Block interaction if pointer over UI
        if (EventSystem.current.IsPointerOverGameObject()) return;

        // Block if it’s not my turn or free spell already used
        if (!SpellRules.CanCastNow(MySide)) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2Int cell = WorldToCell(mouseWorld);

            if (!ChessBoard.Instance.IsInsideBoard(cell)) return;

            ChessPiece piece = ChessBoard.Instance.GetPieceAt(cell);

            // Step 1: select your own piece only
            if (piece != null && piece.team == MySide)
            {
                selectedPiece = piece;

                Debug.Log("Selected " + piece.name);

                TeleportVFX.Instance?.PlayAt(
                    piece.transform.position
                );

                if (activePrompt != null)
                {
                    activePrompt.SetMessage(
                        "Select an empty square."
                    );
                }
            }

            // Step 2: choose empty destination
            if (piece == null)
            {
                if (selectedPiece == null)
                {
                    Debug.Log("Select a piece first.");
                    return;
                }

                Teleport(selectedPiece, cell);
            }
            else
            {
                Debug.Log("Target cell is not empty.");
            }
        }
    }

    void Teleport(ChessPiece piece, Vector2Int targetCell)
    {
        // Networked path: ask server; clients will sync via RPC
        if (Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            GameState.Instance.TeleportPieceServerRpc(piece.Id, targetCell.x, targetCell.y);

            // Consume the free spell ONLY if it’s my turn (it is, but keep invariant)
            if (TurnManager.Instance.IsPlayersTurn(piece.team))
                if (!isMageAbility)
                {
                    if (TurnManager.Instance != null &&
                        TurnManager.Instance.IsPlayersTurn(MySide))
                    {
                        TurnManager.Instance.RegisterFreeSpellCast();
                    }
                }
            return;
        }

        // Offline fallback
        Vector3 origin = piece.transform.position;
        Vector3 world = BoardInitializer.Instance.GetWorldPosition(targetCell);

        // (UI already guaranteed empty cell)
        ChessBoard.Instance.MovePiece(piece.currentCell, targetCell);
        piece.SetPosition(targetCell, world);
        piece.hasMoved = true;

        bool explosiveTrapTriggered = ChessBoard.Instance.TryConsumeEnemyExplosiveTrap(
            targetCell,
            piece.team,
            out _
        );

        if (explosiveTrapTriggered)
        {
            ChessBoard.Instance.HideExplosiveTrapMarker(targetCell);
            ChessBoard.Instance.PlayExplosiveTrapEffect(targetCell);
            ChessBoard.Instance.CapturePiece(targetCell);
        }
        else
        {
            TeleportVFX.Instance?.PlayJump(
                origin,
                piece.transform.position
            );

            // Teleported piece cannot move again this turn.
            piece.ApplyStunOneTurn();
        }


        if (!isMageAbility &&
            TurnManager.Instance != null &&
            TurnManager.Instance.IsPlayersTurn(piece.team))
        {
            TurnManager.Instance.RegisterFreeSpellCast();
        }

        Debug.Log(explosiveTrapTriggered
           ? "Teleport landed on an explosive trap at " + targetCell
           : "Teleported to " + targetCell);
        CloseSuccess(); // close spell UI
    }
    public void CancelSpell()
    {
        if (hasClosed)
            return;

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

    private void CloseSuccess()
    {
        if (hasClosed)
            return;

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
    public void ConfigureAsMageAbility(
    System.Action onSuccess,
    System.Action onCancel)
    {
        isMageAbility = true;

        mageAbilitySuccess = onSuccess;
        mageAbilityCancel = onCancel;

        Debug.Log(
            "[TELEPORT] Configured as Portal Mage ability."
        );
    }
    private void OnDestroy()
    {
        if (hasClosed)
            return;

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
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        const float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }

}
