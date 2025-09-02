using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ChessBoard;

public class ResurrectionPanel : MonoBehaviour
{
    public GameObject buttonPrefab; // A UI button prefab with image
    public Transform whiteContainer;
    public Transform blackContainer;

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        PopulateButtons(TeamColor.White, whiteContainer);
        PopulateButtons(TeamColor.Black, blackContainer);
    }

    void PopulateButtons(TeamColor team, Transform container)
    {
        // Clear previous buttons
        foreach (Transform child in container)
            Destroy(child.gameObject);

        List<CapturedPieceData> dead = ChessBoard.Instance.graveyard.GetCapturedByTeam(team);

        foreach (var pieceData in dead)
        {
            GameObject btnObj = Instantiate(buttonPrefab, container);
            Button btn = btnObj.GetComponent<Button>();
            Image img = btnObj.GetComponent<Image>();

            if (pieceData.pieceSprite != null)
                img.sprite = pieceData.pieceSprite;
            else
                Debug.LogWarning($"Missing sprite for {pieceData.pieceType}");

            btn.onClick.AddListener(() => OnResurrectClicked(pieceData));
        }
    }

    void OnResurrectClicked(CapturedPieceData data)
    {
        // Use stored original position or fallback to nearest available square
        Vector2Int spawn = data.originalPosition;
        if (!ChessBoard.Instance.IsInsideBoard(spawn) || ChessBoard.Instance.GetPieceAt(spawn) != null)
        {
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);
        }

        GameObject resurrected = Instantiate(data.originalPrefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();
        newPiece.team = data.team;
        newPiece.pieceType = data.pieceType;
        newPiece.pieceSprite = data.pieceSprite;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        newPiece.MarkAsResurrected();//for visual only for now

        ChessBoard.Instance.PlacePiece(newPiece, spawn);
        ChessBoard.Instance.graveyard.RemoveCapturedPiece(data);

        TurnManager.Instance.NextTurn();
        Destroy(gameObject); // Close panel
    }
}

