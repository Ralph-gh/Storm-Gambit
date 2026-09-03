using UnityEngine;
using UnityEngine.EventSystems;

public class TapMoveController : MonoBehaviour
{
    private ChessPiece selectedPiece;

    [Header("Tap Settings")]
    [SerializeField] private float maxTapMovement = 20f;

    private Vector2 pointerDownPosition;
    private bool trackingTap;

    void Update()
    {
        // ================================
        // MOBILE TOUCH
        // ================================

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // Don't treat UI buttons/cards as board taps.
                if (EventSystem.current != null &&
                    EventSystem.current.IsPointerOverGameObject(
                        touch.fingerId))
                {
                    trackingTap = false;
                    return;
                }

                pointerDownPosition = touch.position;
                trackingTap = true;
            }

            if (touch.phase == TouchPhase.Ended &&
                trackingTap)
            {
                trackingTap = false;

                float movement =
                    Vector2.Distance(
                        pointerDownPosition,
                        touch.position);

                // Finger moved too much = drag, NOT tap.
                if (movement > maxTapMovement)
                    return;

                HandleTap(touch.position);
            }

            return;
        }


        // ================================
        // MOUSE / UNITY EDITOR
        // ================================

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                trackingTap = false;
                return;
            }

            pointerDownPosition = Input.mousePosition;
            trackingTap = true;
        }

        if (Input.GetMouseButtonUp(0) &&
            trackingTap)
        {
            trackingTap = false;

            Vector2 releasePosition =
                Input.mousePosition;

            float movement =
                Vector2.Distance(
                    pointerDownPosition,
                    releasePosition);

            // Real drag happened.
            if (movement > maxTapMovement)
                return;

            HandleTap(releasePosition);
        }
    }


    private void HandleTap(Vector2 screenPosition)
    {
        if (ChessBoard.Instance == null ||
            BoardInitializer.Instance == null ||
            Camera.main == null)
            return;

        Vector3 world =
            Camera.main.ScreenToWorldPoint(screenPosition);

        world.z = 0f;

        // Use the actual Tilemap instead of assuming board coordinates.
        Vector3Int tileCell =
            BoardInitializer.Instance.tilemap.WorldToCell(world);

        Vector2Int cell =
            new Vector2Int(
                tileCell.x,
                tileCell.y);

        if (!ChessBoard.Instance.IsInsideBoard(cell))
        {
            selectedPiece = null;
            return;
        }

        ChessPiece tappedPiece =
            ChessBoard.Instance.GetPieceAt(cell);


        // ============================================
        // NOTHING SELECTED YET
        // ============================================

        if (selectedPiece == null)
        {
            if (CanSelect(tappedPiece))
            {
                selectedPiece = tappedPiece;

                Debug.Log(
                    $"Selected {selectedPiece.name}");
            }

            return;
        }


        // ============================================
        // TAP SELECTED PIECE AGAIN = CANCEL
        // ============================================

        if (tappedPiece == selectedPiece)
        {
            selectedPiece = null;

            Debug.Log("Selection cancelled.");
            return;
        }


        // ============================================
        // TAP ANOTHER FRIENDLY PIECE = SWITCH
        // ============================================

        if (tappedPiece != null &&
            tappedPiece.team == selectedPiece.team)
        {
            if (CanSelect(tappedPiece))
            {
                selectedPiece = tappedPiece;

                Debug.Log(
                    $"Selected {selectedPiece.name}");
            }

            return;
        }


        // ============================================
        // EMPTY SQUARE OR ENEMY PIECE
        // ============================================

        ChessPiece movingPiece = selectedPiece;

        Vector2Int oldCell =
            movingPiece.currentCell;

        movingPiece.TryMoveFromTap(cell);


        // If the move succeeded, deselect.
        //
        // This also catches promotion because promotion
        // updates currentCell before opening its UI.
        if (movingPiece == null ||
            movingPiece.currentCell != oldCell)
        {
            selectedPiece = null;
        }

        // Invalid move:
        // keep it selected so player can tap another square.
    }


    private bool CanSelect(ChessPiece piece)
    {
        if (piece == null)
            return false;

        if (piece.IsFrozen ||
            piece.IsStunned)
            return false;

        // Multiplayer ownership check
        bool isNet =
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        if (isNet)
        {
            return NetPlayer.Local != null &&
                   NetPlayer.Local.Side.Value == piece.team &&
                   NetPlayer.Local.CanAct();
        }

        // Offline
        return TurnManager.Instance != null &&
               TurnManager.Instance.IsPlayersTurn(piece.team);
    }
}