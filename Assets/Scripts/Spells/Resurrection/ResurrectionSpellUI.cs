using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        List<ChessPiece> capturedPieces = ChessBoard.Instance.graveyard.GetCapturedByTeam(currentTeam);

        Transform container = currentTeam == TeamColor.White ? whiteContainer : blackContainer;

        foreach (ChessPiece piece in capturedPieces)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, container);
            ResurrectionButton resButton = buttonObj.GetComponent<ResurrectionButton>();
            resButton.Initialize(piece, this);
        }
    }

    public void Resurrect(ChessPiece piece)
    {
        Vector2Int spawn = BoardInitializer.Instance.GetStartingSquare(piece);

        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);

        if (spawn.x == -1)
        {
            Debug.Log("No available square to resurrect.");
            return;
        }

        GameObject resurrected = Instantiate(piece.originalPrefab, BoardInitializer.Instance.GetWorldPosition(spawn), Quaternion.identity);
        ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();

        newPiece.team = piece.team;
        newPiece.pieceType = piece.pieceType;
        newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));

        ChessBoard.Instance.PlacePiece(newPiece, spawn);
        ChessBoard.Instance.graveyard.RemoveCapturedPiece(piece.team, piece);

        TurnManager.Instance.NextTurn();
        Destroy(gameObject); // close UI
    }
}
