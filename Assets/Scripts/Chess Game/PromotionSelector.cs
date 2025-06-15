using UnityEngine;
using UnityEngine.UI;

public class PromotionSelector : MonoBehaviour
{
    public Button queenButton, rookButton, bishopButton, knightButton;
    public Image queenImage, rookImage, bishopImage, knightImage;
    public Sprite whiteQueen, blackQueen, whiteRook, blackRook, whiteBishop, blackBishop, whiteKnight, blackKnight;
    private Vector3 worldPosition;
    private Vector2Int cellPosition;
    private TeamColor teamColor;

    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return BoardInitializer.Instance.tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    public void Show(Vector3 position, Vector2Int cell, TeamColor team)
    {
        worldPosition = position;
        cellPosition = cell;
        teamColor = team;

        transform.position = Camera.main.WorldToScreenPoint(position);
        SetImagesForTeam(team);
        gameObject.SetActive(true);
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
        GameObject prefab = ChessBoard.Instance.initializer.GetPromotionPrefab(type, teamColor);

        if (prefab != null)
        {
            Vector3 snappedWorldPosition = BoardInitializer.Instance.GetWorldPosition(cellPosition);
            GameObject newPieceGO = Instantiate(prefab, worldPosition, Quaternion.identity);
            ChessPiece newPiece = newPieceGO.GetComponent<ChessPiece>();
            newPiece.SetPosition(cellPosition, worldPosition);
            newPiece.team = teamColor;
            ChessBoard.Instance.PlacePiece(newPiece, cellPosition);
        }

        Destroy(ChessBoard.Instance.pawnToPromote.gameObject);
        ChessBoard.Instance.pawnToPromote = null;
        gameObject.SetActive(false);
    }
}
