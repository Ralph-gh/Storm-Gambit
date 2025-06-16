using UnityEngine;
using System.Collections.Generic;

public class ResurrectionSpell : ISpell
{
    public string Name => "Resurrection Stone";

    public bool CanCast(TeamColor casterTeam)
    {
        // You can add mana limits or timing rules here later
        return ChessBoard.Instance.HasCapturedPieces(casterTeam);
    }

    public void Cast(TeamColor casterTeam)
    {
        List<ChessPiece> captured = ChessBoard.Instance.GetCapturedPieces(casterTeam);
        if (captured.Count == 0)
        {
            Debug.Log("No pieces to resurrect.");
            return;
        }

        ChessPiece pieceToResurrect = ChoosePieceToResurrect(captured); // UI or default choice
        Vector2Int startingSquare = pieceToResurrect.GetStartingCell();

        Vector2Int targetSquare = ChessBoard.Instance.FindNearestAvailableSquare(startingSquare);
        if (targetSquare == Vector2Int.one * -1)
        {
            Debug.Log("No valid square found to place the piece.");
            return;
        }

        Vector3 worldPos = BoardInitializer.Instance.GetWorldPosition(targetSquare);
        GameObject newPieceGO = GameObject.Instantiate(pieceToResurrect.originalPrefab, worldPos, Quaternion.identity);

        ChessPiece newPiece = newPieceGO.GetComponent<ChessPiece>();
        newPiece.SetPosition(targetSquare, worldPos);
        newPiece.team = casterTeam;

        ChessBoard.Instance.PlacePiece(newPiece, targetSquare);
        ChessBoard.Instance.RemoveCapturedPiece(casterTeam, pieceToResurrect);

        Debug.Log($"Resurrected {pieceToResurrect.name} at {targetSquare}");
    }

    private ChessPiece ChoosePieceToResurrect(List<ChessPiece> captured)
    {
        // TODO: Hook this to UI; for now, return first
        return captured[0];
    }
}
