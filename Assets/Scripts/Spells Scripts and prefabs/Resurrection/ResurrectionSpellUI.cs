using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ChessBoard;

public class ResurrectionSpellUI : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform whiteContainer;
    public Transform blackContainer;

    void Start()
    {
        GenerateButtons();
    }

    void GenerateButtons()
    {
        TeamColor currentTeam = TurnManager.Instance.currentTurn;
        List<CapturedPieceData> captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(currentTeam);

        Transform container = currentTeam == TeamColor.White ? whiteContainer : blackContainer;

        foreach (CapturedPieceData data in captured)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, container);
            ResurrectionButton resButton = buttonObj.GetComponent<ResurrectionButton>();
            resButton.Initialize(data, this);
        }
    }

    public void Resurrect(CapturedPieceData data)
    {
        Vector2Int spawn = data.originalPosition;
        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);

        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);

        if (spawn.x == -1)
        {
            Debug.Log("No available square to resurrect.");
            return;
        }

        GameObject resurrected = Instantiate(data.originalPrefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();

        newPiece.team = data.team;
        newPiece.pieceType = data.pieceType;
        newPiece.pieceSprite = data.pieceSprite;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));

        ChessBoard.Instance.PlacePiece(newPiece, spawn);
        ChessBoard.Instance.graveyard.RemoveCapturedPiece(data);
        TurnManager.Instance.NextTurn();
        Destroy(gameObject); // close UI
    }
}
