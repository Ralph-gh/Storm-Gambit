using UnityEngine;
using UnityEngine.EventSystems;

public class ExplosiveTrapSpellUI : MonoBehaviour
{
    private CardUI sourceCard;
    private bool hasClosed;

    public void BindSourceCard(CardUI card)
    {
        sourceCard = card;
    }

    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local != null)
            ? NetPlayer.Local.Side.Value
            : TurnManager.Instance.currentTurn;

    private void Start()
    {
        Debug.Log("Explosive Trap: choose an empty square on your half of the board.");

        if (sourceCard == null)
            Debug.LogWarning("ExplosiveTrapSpellUI was not bound to its source CardUI. The card will not be consumed after placement.");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSpell();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!SpellRules.CanCastNow(MySide))
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Explosive Trap requires a camera tagged MainCamera.");
            return;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2Int cell = WorldToCell(mouseWorld);

        if (!ChessBoard.Instance.IsInsideBoard(cell))
            return;

        if (!ChessBoard.Instance.IsValidExplosiveTrapCell(cell, MySide))
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
        bool isNetworked = Unity.Netcode.NetworkManager.Singleton != null &&
                           Unity.Netcode.NetworkManager.Singleton.IsListening;

        if (isNetworked)
        {
            GameState.Instance.PlaceExplosiveTrapServerRpc(cell.x, cell.y);

            // Temporary client-side consumption. Later, consume only after a
            // server confirmation RPC says placement succeeded.
            if (TurnManager.Instance.IsPlayersTurn(MySide))
                TurnManager.Instance.RegisterFreeSpellCast();

            CloseSuccess();
            return;
        }

        if (!ChessBoard.Instance.TryPlaceExplosiveTrap(cell, MySide))
            return;

        ChessBoard.Instance.ShowExplosiveTrapMarker(cell);

        if (TurnManager.Instance.IsPlayersTurn(MySide))
            TurnManager.Instance.RegisterFreeSpellCast();

        Debug.Log($"{MySide} placed an explosive trap at {cell}.");
        CloseSuccess();
    }

    public void CancelSpell()
    {
        if (hasClosed) return;

        hasClosed = true;
        sourceCard?.CancelPendingSpellCast();
        Destroy(gameObject);
    }

    private void CloseSuccess()
    {
        if (hasClosed) return;

        hasClosed = true;
        sourceCard?.ConsumeCardAfterSuccessfulCast();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!hasClosed)
            sourceCard?.CancelPendingSpellCast();
    }

    private static Vector2Int WorldToCell(Vector3 world)
    {
        const float cellSize = 0.5f;
        return new Vector2Int(
            Mathf.FloorToInt(world.x / cellSize),
            Mathf.FloorToInt(world.y / cellSize)
        );
    }
}