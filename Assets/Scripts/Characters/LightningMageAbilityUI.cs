using UnityEngine;
using Unity.Netcode;

public class LightningMageAbilityUI : MonoBehaviour
{
    private SpellPromptPanelUI activePrompt;

    private bool hasClosed;
    private bool resolving;

    private System.Action mageAbilitySuccess;
    private System.Action mageAbilityCancel;

    private TeamColor MySide =>
        (NetworkManager.Singleton &&
         NetworkManager.Singleton.IsListening &&
         NetPlayer.Local != null)
            ? NetPlayer.Local.Side.Value
            : TurnManager.Instance.currentTurn;

    void Start()
    {
        Debug.Log("[LIGHTNING MAGE] Ability opened.");

        if (SpellOverlayManager.Instance != null)
        {
            activePrompt =
                SpellOverlayManager.Instance.ShowActionPrompt(
                    "Select a pawn to destroy.",
                    CancelAbility
                );
        }
        else
        {
            Debug.LogWarning(
                "[LIGHTNING MAGE] SpellOverlayManager not found."
            );
        }
    }

    public void ConfigureAsMageAbility(
        System.Action onSuccess,
        System.Action onCancel)
    {
        mageAbilitySuccess = onSuccess;
        mageAbilityCancel = onCancel;
    }

    void Update()
    {
        if (resolving || hasClosed)
            return;

        // Cancel
        if (Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            CancelAbility();
            return;
        }

        // Ability can only be used during my turn.
        if (!IsMyTurn())
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (Camera.main == null ||
            ChessBoard.Instance == null)
            return;

        Vector3 mouseWorld =
            Camera.main.ScreenToWorldPoint(Input.mousePosition);

        mouseWorld.z = 0f;

        Vector2Int cell = WorldToCell(mouseWorld);

        if (!ChessBoard.Instance.IsInsideBoard(cell))
            return;

        ChessPiece target =
            ChessBoard.Instance.GetPieceAt(cell);

        // Empty square
        if (target == null)
        {
            activePrompt?.SetMessage(
                "Select a pawn to destroy."
            );

            return;
        }

        // Any OTHER piece = failed target.
        if (target.pieceType != PieceType.Pawn)
        {
            Debug.Log(
                "[LIGHTNING MAGE] Invalid target: target is not a pawn."
            );

            activePrompt?.SetMessage(
                "Invalid target. Lightning Mage can destroy pawns only."
            );

            return;
        }

        // Respect existing destruction immunity.
        if (target.IsFrozen ||
            target.IsDivinelyProtected)
        {
            Debug.Log(
                "[LIGHTNING MAGE] Pawn is protected from destruction."
            );

            activePrompt?.SetMessage(
                "That pawn is protected from destruction."
            );

            return;
        }

        StrikePawn(target);
    }

    private void StrikePawn(ChessPiece pawn)
    {
        if (pawn == null ||
            pawn.pieceType != PieceType.Pawn)
            return;

        resolving = true;

        activePrompt?.SetMessage(
            "Lightning incoming..."
        );

        // =============================
        // NETWORK
        // =============================
        if (NetworkManager.Singleton &&
            NetworkManager.Singleton.IsListening)
        {
            if (GameState.Instance == null)
            {
                resolving = false;
                return;
            }

            GameState.Instance.LightningDestroyPawnRpc(pawn.Id);

            // Same mage behaviour as Portal/Frost:
            // successful target selection spends the ability,
            // but DOES NOT consume the normal free spell.
            CloseSuccess();

            return;
        }

        // =============================
        // OFFLINE
        // =============================

        Vector2Int cell = pawn.currentCell;

        if (LightningStrikeVFX.Instance != null)
        {
            LightningStrikeVFX.Instance.PlayStrike(
                pawn,
                () =>
                {
                    if (pawn != null &&
                        ChessBoard.Instance.GetPieceAt(cell) == pawn)
                    {
                        ChessBoard.Instance.CapturePiece(cell);
                    }

                    CloseSuccess();
                }
            );
        }
        else
        {
            Debug.LogWarning(
                "[LIGHTNING MAGE] LightningStrikeVFX missing. " +
                "Destroying pawn immediately."
            );

            ChessBoard.Instance.CapturePiece(cell);

            CloseSuccess();
        }
    }

    private bool IsMyTurn()
    {
        if (NetworkManager.Singleton &&
            NetworkManager.Singleton.IsListening)
        {
            return NetPlayer.Local != null &&
                   GameState.Instance != null &&
                   GameState.Instance.CurrentTurn.Value ==
                   NetPlayer.Local.Side.Value;
        }

        return TurnManager.Instance != null &&
               TurnManager.Instance.IsPlayersTurn(MySide);
    }

    public void CancelAbility()
    {
        if (hasClosed || resolving)
            return;

        hasClosed = true;

        if (activePrompt != null)
        {
            activePrompt.Close();
            activePrompt = null;
        }

        mageAbilityCancel?.Invoke();

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

        mageAbilitySuccess?.Invoke();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (hasClosed)
            return;

        activePrompt?.Close();

        mageAbilityCancel?.Invoke();
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        const float size = 0.5f;

        return new Vector2Int(
            Mathf.FloorToInt(world.x / size),
            Mathf.FloorToInt(world.y / size)
        );
    }
}