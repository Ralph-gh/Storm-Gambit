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
    public void BindSourceCard(CardUI card) => sourceCard = card;

    private CardUI sourceCard;
    private bool hasClosed;
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
            CloseSuccess(); // close UI and destroy card upon success
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
        // Get the prefab stored with the captured piece.
        // Fallback to BoardInitializer in case an older graveyard entry
        // was created before originalPrefab was preserved.
        GameObject prefab = data.originalPrefab;

        if (prefab == null && BoardInitializer.Instance != null)
        {
            prefab = BoardInitializer.Instance.GetPrefab(
                data.team,
                data.pieceType
            );
        }

        if (prefab == null)
        {
            Debug.LogError(
                $"[RESURRECTION] No prefab found for {data.team} {data.pieceType}."
            );
            return;
        }

        GameObject resurrected = Instantiate(
            prefab,
            BoardInitializer.Instance.GetWorldPosition(spawn),
            Quaternion.identity
        );

        ChessPiece newPiece = resurrected.GetComponent<ChessPiece>();

        newPiece.team = data.team;
        newPiece.pieceType = data.pieceType;
        newPiece.pieceSprite = data.pieceSprite;

        // IMPORTANT:
        // Preserve resurrection information if this piece is captured again.
        newPiece.originalPrefab = prefab;
        newPiece.startingCell = data.originalPosition;

        newPiece.SetPosition(
            spawn,
            BoardInitializer.Instance.GetWorldPosition(spawn)
        );

        newPiece.MarkAsResurrected();

        BoardFlipController.Instance?.ApplyOrientation(newPiece);

        ChessBoard.Instance.PlacePiece(newPiece, spawn);

        // Use ChessBoard wrapper so OnGraveyardChanged is raised.
        ChessBoard.Instance.RemoveCapturedPiece(data);

        if (TurnManager.Instance.IsPlayersTurn(data.team))
                TurnManager.Instance.RegisterFreeSpellCast();

        CloseSuccess(); // close UI and destroy card upon success

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
}