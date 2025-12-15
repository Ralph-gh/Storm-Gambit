using UnityEngine;
using UnityEngine.EventSystems;

public class TeleportationSpellUI : MonoBehaviour
{
    private ChessPiece selectedPiece = null;
    private CardUI sourceCard;
    private bool hasClosed;

    public void BindSourceCard(CardUI card) => sourceCard = card;
    // Resolve my side for net/offline once per frame
    private TeamColor MySide =>
        (SpellRules.IsNet && NetPlayer.Local) ? NetPlayer.Local.Side.Value
                                              : TurnManager.Instance.currentTurn;

    void Start()
    {
        Debug.Log("Teleportation Spell UI instantiated and active.");
    }

    void Update()
    {
        // Cancel with right-click or Esc
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Destroy(gameObject);
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
            if (selectedPiece == null)
            {
                if (piece != null && piece.team == MySide)
                {
                    selectedPiece = piece;
                    Debug.Log("Selected " + piece.name);
                    TeleportVFX.Instance?.PlayAt(piece.transform.position); // selection ping
                }
                return;
            }

            // Step 2: choose empty destination
            if (piece == null)
            {
                Teleport(selectedPiece, cell);
                CloseSuccess(); // close spell UI
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
                TurnManager.Instance.RegisterFreeSpellCast();
            return;
        }

        // Offline fallback
        Vector3 origin = piece.transform.position;
        Vector3 world = BoardInitializer.Instance.GetWorldPosition(targetCell);

        // (UI already guaranteed empty cell)
        ChessBoard.Instance.MovePiece(piece.currentCell, targetCell);
        piece.SetPosition(targetCell, world);
        piece.hasMoved = true;

        TeleportVFX.Instance?.PlayJump(origin, piece.transform.position);

        if (TurnManager.Instance.IsPlayersTurn(piece.team))
            TurnManager.Instance.RegisterFreeSpellCast();

        Debug.Log("Teleported to " + targetCell);
        CloseSuccess(); // close spell UI
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
    Vector2Int WorldToCell(Vector3 world)
    {
        const float size = 0.5f;
        return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.y / size));
    }
}
