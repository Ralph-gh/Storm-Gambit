using UnityEngine;
using UnityEngine.EventSystems;

public class ExplosiveTrapSpellUI : MonoBehaviour
{
    private CardUI sourceCard;
    private SpellPromptPanelUI activePrompt;

    private bool hasClosed;

    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local != null)
            ? NetPlayer.Local.Side.Value
            : TurnManager.Instance.currentTurn;

    public void BindSourceCard(CardUI card)
    {
        sourceCard = card;
    }

    private void Start()
    {
        Debug.Log(
            "Explosive Trap: choose an empty square on your half of the board."
        );

        if (SpellOverlayManager.Instance != null)
        {
            activePrompt =
                SpellOverlayManager.Instance.ShowActionPrompt(
                    "Select a square to place a trap.",
                    CancelSpell
                );
        }
        else
        {
            Debug.LogWarning(
                "[TRAP] SpellOverlayManager was not found."
            );
        }
    }

    private void Update()
    {
        // Desktop cancellation.
        if (Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSpell();
            return;
        }

        // Clicking UI should not count as choosing a board square.
        // Your HandBlocker therefore blocks the hand while leaving
        // the actual chessboard clickable.
      

        if (!SpellRules.CanCastNow(MySide))
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (Camera.main == null)
            return;

        Vector3 mouseWorld =
            Camera.main.ScreenToWorldPoint(Input.mousePosition);

        mouseWorld.z = 0f;

        Vector2Int cell =
            WorldToCell(mouseWorld);

        if (!ChessBoard.Instance.IsInsideBoard(cell))
            return;

        if (!ChessBoard.Instance.IsValidExplosiveTrapCell(
                cell,
                MySide))
        {
            Debug.Log(
                MySide == TeamColor.White
                    ? "White must place the trap on an empty square in rows 1-4."
                    : "Black must place the trap on an empty square in rows 5-8."
            );

            return;
        }

        PlaceTrap(cell);
    }

    private void PlaceTrap(Vector2Int cell)
    {
        bool isNetworked =
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        // ============================
        // MULTIPLAYER
        // ============================

        if (isNetworked)
        {
            if (GameState.Instance == null)
            {
                Debug.LogError(
                    "[TRAP] GameState.Instance is missing."
                );

                return;
            }

            GameState.Instance.PlaceExplosiveTrapServerRpc(
                cell.x,
                cell.y
            );

            if (TurnManager.Instance != null &&
                TurnManager.Instance.IsPlayersTurn(MySide))
            {
                TurnManager.Instance.RegisterFreeSpellCast();
            }

            Debug.Log(
                $"{MySide} requested explosive trap placement at {cell}."
            );

            CloseSuccess();
            return;
        }

        // ============================
        // SINGLE PLAYER
        // ============================

        if (!ChessBoard.Instance.TryPlaceExplosiveTrap(
                cell,
                MySide))
        {
            return;
        }

        ChessBoard.Instance.ShowExplosiveTrapMarker(cell);

        if (TurnManager.Instance != null &&
            TurnManager.Instance.IsPlayersTurn(MySide))
        {
            TurnManager.Instance.RegisterFreeSpellCast();
        }

        Debug.Log(
            $"{MySide} placed an explosive trap at {cell}."
        );

        CloseSuccess();
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

        sourceCard?.CancelPendingSpellCast();

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

        sourceCard?.ConsumeCardAfterSuccessfulCast();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // This catches unexpected destruction.
        if (hasClosed)
            return;

        if (activePrompt != null)
        {
            activePrompt.Close();
            activePrompt = null;
        }

        sourceCard?.CancelPendingSpellCast();
    }

    private static Vector2Int WorldToCell(
        Vector3 world)
    {
        const float cellSize = 0.5f;

        return new Vector2Int(
            Mathf.FloorToInt(world.x / cellSize),
            Mathf.FloorToInt(world.y / cellSize)
        );
    }
}