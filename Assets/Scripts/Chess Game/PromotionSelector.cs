using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PromotionSelector : MonoBehaviour
{
    public Button queenButton, rookButton, bishopButton, knightButton;
    public Image queenImage, rookImage, bishopImage, knightImage;
    public Sprite whiteQueen, blackQueen, whiteRook, blackRook, whiteBishop, blackBishop, whiteKnight, blackKnight;
    private Vector3 worldPosition;
    private Vector2Int cellPosition;
    private TeamColor teamColor;
    private KeepUIOnScreen keepUIOnScreen;

    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return BoardInitializer.Instance.tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }
    private void Awake()
    {
        keepUIOnScreen = GetComponent<KeepUIOnScreen>();
    }
 public void Show(Vector3 position, Vector2Int cell, TeamColor team)
{
    worldPosition = position;
    cellPosition = cell;
    teamColor = team;

    SetImagesForTeam(team);

    gameObject.SetActive(true);

    // Position beside the pawn.
    transform.position =
        Camera.main.WorldToScreenPoint(position);

    // Make sure Unity has finished calculating the UI size.
    Canvas.ForceUpdateCanvases();

    // Safety: if any part went outside the screen,
    // push the promotion panel back inside.
    if (keepUIOnScreen != null)
        keepUIOnScreen.ClampNow();
}

    private void SetImagesForTeam(TeamColor team)
    {
        if (team == TeamColor.White)
        {
            queenImage.sprite = whiteQueen;
            rookImage.sprite = whiteRook;
            bishopImage.sprite = whiteBishop;
            knightImage.sprite = whiteKnight;
        }
        else
        {
            queenImage.sprite = blackQueen;
            rookImage.sprite = blackRook;
            bishopImage.sprite = blackBishop;
            knightImage.sprite = blackKnight;
        }
    }


    public void PromoteTo(string type)
    {
        // =========================================================
        // NETWORK PROMOTION
        // =========================================================

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (ChessBoard.Instance.pawnToPromote == null)
            {
                Debug.LogWarning(
                    "[PROMOTION] No pawn is waiting for promotion."
                );

                return;
            }
            PieceType chosenType;

            switch (type)
            {
                case "Queen":
                    chosenType = PieceType.Queen;
                    break;

                case "Rook":
                    chosenType = PieceType.Rook;
                    break;

                case "Bishop":
                    chosenType = PieceType.Bishop;
                    break;

                case "Knight":
                    chosenType = PieceType.Knight;
                    break;

                default:
                    Debug.LogWarning(
                        $"[PROMOTION] Invalid promotion type: {type}"
                    );

                    return;
            }

            GameState.Instance.RequestPromotionServerRpc(
                ChessBoard.Instance.pawnToPromote.Id,
                chosenType
            );

            // Hide immediately to prevent double-clicking.
            gameObject.SetActive(false);

            return;
        }

        // =========================================================
        // EXISTING OFFLINE PROMOTION
        // =========================================================

        GameObject prefab =
            ChessBoard.Instance.initializer
                .GetPromotionPrefab(type, teamColor);

        if (prefab != null)
        {
            Vector3 snappedWorldPosition =
                BoardInitializer.Instance
                    .GetWorldPosition(cellPosition);

            GameObject newPieceGO =
                Instantiate(
                    prefab,
                    worldPosition,
                    Quaternion.identity
                );

            ChessPiece newPiece =
                newPieceGO.GetComponent<ChessPiece>();

            newPiece.SetPosition(
                cellPosition,
                worldPosition
            );

            newPiece.team = teamColor;

            ChessBoard.Instance.PlacePiece(
                newPiece,
                cellPosition
            );
        }

        Destroy(
            ChessBoard.Instance
                .pawnToPromote
                .gameObject
        );

        ChessBoard.Instance.pawnToPromote = null;

        TurnManager.Instance.NextTurn();

        gameObject.SetActive(false);
    }
}
