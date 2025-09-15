using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.UI.Image;

public class TeleportationSpellUI : MonoBehaviour
{
    private ChessPiece selectedPiece = null;

    void Start()
    {
        Debug.Log("Teleportation Spell UI instantiated and active.");
    }

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
                    TeleportVFX.Instance?.PlayAt(piece.transform.position); // selection ping
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
        Vector3 origin = piece.transform.position;
        Vector3 world = BoardInitializer.Instance.GetWorldPosition(targetCell);
        ChessBoard.Instance.MovePiece(piece.currentCell, targetCell);
        piece.SetPosition(targetCell, world);
        // Balance/logic: teleported piece should count as having moved this turn
        piece.hasMoved = true;
        // after move:
        Vector3 dest = piece.transform.position;
        TeleportVFX.Instance?.PlayJump(origin, dest);
        var myTeam = piece.team;
        if (TurnManager.Instance.IsPlayersTurn(myTeam))
            TurnManager.Instance.RegisterFreeSpellCast();


        Debug.Log("Teleported to " + targetCell);
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }
}
