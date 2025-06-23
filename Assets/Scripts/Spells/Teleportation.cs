using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportationSpell : MonoBehaviour, ISpell
{
    private TeamColor caster;
    private ChessPiece selectedPiece;
    public string Name => "Teleportation";
    public GameObject cardObject; // Set this from CardUI when casting

    public bool CanCast(TeamColor casterTeam)
    {
        // More to be added later
        return !ChessBoard.Instance.gameOver;
    }

    public void Cast(TeamColor team)
    {
        caster = team;
        SpellUIManager.Instance.ShowMessage("Select a piece to teleport...");
        SpellInputManager.Instance.EnablePieceSelection(OnPieceSelected);
    }

    void OnPieceSelected(ChessPiece piece)
    {
        if (piece.team != caster)
        {
            SpellUIManager.Instance.ShowMessage("Invalid piece. Try again.");
            return;
        }

        selectedPiece = piece;
        SpellUIManager.Instance.ShowMessage("Select an empty square...");
        SpellInputManager.Instance.EnableSquareSelection(OnSquareSelected);
    }

    void OnSquareSelected(Vector2Int cell)
    {
        if (ChessBoard.Instance.GetPieceAt(cell) != null)
        {
            SpellUIManager.Instance.ShowMessage("Square is occupied.");
            return;
        }

        SpellUIManager.Instance.ShowTeleportConfirm(() =>
        {
            Vector3 worldPos = BoardInitializer.Instance.GetWorldPosition(cell);
            selectedPiece.transform.position = worldPos;
            selectedPiece.currentCell = cell;
            ChessBoard.Instance.PlacePiece(selectedPiece, cell);
            //GameManager.Instance.Play("Teleport"); to be implemented later

            if (cardObject != null)
                Destroy(cardObject);
        });
    }
}