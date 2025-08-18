using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        List<ChessPiece> dead = ChessBoard.Instance.graveyard.GetCapturedByTeam(team);
        foreach (var piece in dead)
        {
            GameObject btnObj = Instantiate(buttonPrefab, container);
            Button btn = btnObj.GetComponent<Button>();
            Image img = btnObj.GetComponent<Image>();

            img.sprite = piece.GetComponent<SpriteRenderer>().sprite; // Or assign a proper icon
            btn.onClick.AddListener(() => OnResurrectClicked(piece));
        }
    }

    void OnResurrectClicked(ChessPiece piece)
    {
        // Logic to find original spawn or closest available square
        Vector2Int spawn = BoardInitializer.Instance.GetStartingSquare(piece);
        if (!ChessBoard.Instance.IsInsideBoard(spawn) || ChessBoard.Instance.GetPieceAt(spawn) != null)
        {
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);
        }

        GameObject resurrected = Instantiate(BoardInitializer.Instance.GetPiecePrefab(piece), BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();
        newPiece.team = piece.team;
        newPiece.pieceType = piece.pieceType;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
        ChessBoard.Instance.PlacePiece(newPiece, spawn);
        ChessBoard.Instance.graveyard.RemoveCapturedPiece(piece.team, piece);

        TurnManager.Instance.NextTurn();
        Destroy(gameObject); // Close panel
    }
}
