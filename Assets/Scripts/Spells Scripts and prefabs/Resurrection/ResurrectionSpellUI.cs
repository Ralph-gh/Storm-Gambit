using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static ChessBoard;

public class ResurrectionSpellUI : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform whiteContainer;
    public Transform blackContainer;

    void OnEnable() => GenerateButtons();

    void GenerateButtons()
    {
        TeamColor mySide = (SpellRules.IsNet && NetPlayer.Local != null)
         ? NetPlayer.Local.Side.Value
         : TurnManager.Instance.currentTurn;
        TeamColor currentTeam = TurnManager.Instance.currentTurn;
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening && NetPlayer.Local != null)
        {
            if (NetPlayer.Local.Side.Value != currentTeam)
                return;
        }
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
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
        {
            GameState.Instance.ResurrectServerRpc(
                data.team,
                data.pieceType,
                data.originalPosition.x,
                data.originalPosition.y
            );
            Destroy(gameObject); // close UI
            return;
        }
        Vector2Int spawn = data.originalPosition;

        if (ChessBoard.Instance.GetPieceAt(spawn) != null)
            spawn = ChessBoard.Instance.FindNearestAvailableSquare(spawn);

        if (spawn.x == -1)
        {
            Debug.Log("No available square to resurrect.");
            return;
        }
        GameObject resurrected = Instantiate(
           data.originalPrefab,
           BoardInitializer.Instance.GetWorldPosition(spawn),
           Quaternion.identity);
            ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();
            newPiece.team = data.team;
            newPiece.pieceType = data.pieceType;
            newPiece.pieceSprite = data.pieceSprite;

            newPiece.SetPosition(spawn, BoardInitializer.Instance.GetWorldPosition(spawn));
            newPiece.MarkAsResurrected();//Used for visual only inside ChessPiece.cs for now

            ChessBoard.Instance.PlacePiece(newPiece, spawn);
            ChessBoard.Instance.graveyard.RemoveCapturedPiece(data);

            if (TurnManager.Instance.IsPlayersTurn(data.team))
                TurnManager.Instance.RegisterFreeSpellCast();

            Destroy(gameObject); // close UI
        
    }
}