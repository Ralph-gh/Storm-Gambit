using UnityEngine;
using UnityEngine.EventSystems;

public class DivineProtectionSpellUI : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Divine Protection UI active.");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = WorldToCell(mouseWorld);

            if (!ChessBoard.Instance.IsInsideBoard(cell)) return;

            ChessPiece piece = ChessBoard.Instance.GetPieceAt(cell);

            // Only allow selecting your own piece on your turn
            if (piece != null && piece.team == TurnManager.Instance.currentTurn)
            {
                piece.ApplyDivineProtectionOneTurn();  // <-- core effect
                //TurnManager.Instance.NextTurn();       commented to no longer end turn
                if (TurnManager.Instance.IsPlayersTurn(piece.team))
                    TurnManager.Instance.RegisterFreeSpellCast();
                Destroy(gameObject);                   // close spell UI
            }
        }
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }
}

