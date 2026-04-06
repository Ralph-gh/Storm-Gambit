using UnityEngine;
using UnityEngine.EventSystems;

public class FreezeSpellUI : MonoBehaviour
{
    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local) ? NetPlayer.Local.Side.Value
                                              : TurnManager.Instance.currentTurn;

    void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Destroy(gameObject);
            return;
        }

        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (!SpellRules.CanCastNow(MySide)) return;

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

    void Freeze(ChessPiece piece)
    {
        if (Unity.Netcode.NetworkManager.Singleton &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            GameState.Instance.ApplyFreezeServerRpc(piece.Id);

            if (TurnManager.Instance.IsPlayersTurn(MySide))
                TurnManager.Instance.RegisterFreeSpellCast();
        }
        else
        {
            piece.ApplyFreeze(2);

            if (TurnManager.Instance.IsPlayersTurn(MySide))
                TurnManager.Instance.RegisterFreeSpellCast();
        }

        Destroy(gameObject);
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        const float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size),
                              Mathf.FloorToInt(world.y / size));
    }
}