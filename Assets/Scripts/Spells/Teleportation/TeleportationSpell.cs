using UnityEngine;
using UnityEngine.EventSystems;

public class TeleportationSpellUI : MonoBehaviour
{
    private ChessPiece selectedPiece = null;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = WorldToCell(mouseWorld);

            if (!ChessBoard.Instance.IsInsideBoard(cell)) return;

            ChessPiece piece = ChessBoard.Instance.GetPieceAt(cell);

            // Step 1: Select your own piece
            if (selectedPiece == null)
            {
                if (piece != null && piece.team == TurnManager.Instance.currentTurn)
                {
                    selectedPiece = piece;
                    Debug.Log("Selected " + piece.name);
                }
            }
            else
            {
                // Step 2: Try to teleport
                if (piece == null)
                {
                    Teleport(selectedPiece, cell);
                    Destroy(gameObject); // close spell UI
                }
                else
                {
                    Debug.Log("Target cell is not empty.");
                }
            }
        }
    }

    void Teleport(ChessPiece piece, Vector2Int targetCell)
    {
        Vector3 world = BoardInitializer.Instance.GetWorldPosition(targetCell);
        ChessBoard.Instance.MovePiece(piece.currentCell, targetCell);
        piece.SetPosition(targetCell, world);
        TurnManager.Instance.NextTurn();
        Debug.Log("Teleported to " + targetCell);
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }
}
