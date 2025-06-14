using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum TeamColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King}
public class ChessPiece : MonoBehaviour
{
    private bool isDragging = false;
    private Vector3 originalPosition;
    private Vector3 offset;
    public TeamColor team;
    public PieceType pieceType;
    public bool hasMoved= false;
    public Vector2Int currentCell;

    public void SetPosition(Vector2Int cellPosition, Vector3 worldPosition)
    {
        currentCell = cellPosition;
        transform.position = worldPosition;
        hasMoved = false; // Reset in case piece is re-used or repositioned
    }
    void OnMouseDown()
    {
        if (!TurnManager.Instance.IsPlayersTurn(team)) return;

        originalPosition = transform.position;
        offset = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        offset.z = 0; // lock to 2D plane
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f; // Force to 2D layer
            transform.position = mousePos + offset;
        }
    }

    void OnMouseUp()
    {
        isDragging = false;

        // Block mouse-up logic if it’s not your turn
        if (!TurnManager.Instance.IsPlayersTurn(team))
        {
            transform.position = originalPosition;
            return;
        }

        // Snap to grid or validate move
        Vector3 snappedPosition = SnapToGrid(transform.position);
        if (IsValidMove(snappedPosition))
        {
            Vector2Int newCell = WorldToCell(snappedPosition);

            ChessPiece target = ChessBoard.Instance.GetPieceAt(newCell);
            if (target != null && target.team != team)
            {
                ChessBoard.Instance.CapturePiece(newCell); //  Use new method
            }
            ChessBoard.Instance.MovePiece(currentCell, newCell);
            currentCell = newCell;

            hasMoved = true; // <-- Mark the piece as having moved
            transform.position = SnapToGrid(transform.position);
            TurnManager.Instance.NextTurn();

            
        }
        else
        {
            transform.position = originalPosition; // Invalid move, reset
        }
        if (IsValidMove(snappedPosition))
        {
            transform.position = snappedPosition;
            currentCell = WorldToCell(snappedPosition);
            TurnManager.Instance.NextTurn();
        }
    }

    Vector3 SnapToGrid(Vector3 rawPosition)
    {
        float cellSize = 0.5f; // Assuming 128px sprites with 256 PPU
        float x = Mathf.Floor(rawPosition.x / cellSize) * cellSize + cellSize / 2f;
        float y = Mathf.Floor(rawPosition.y / cellSize) * cellSize + cellSize / 2f;
        return new Vector3(x, y, 0f);
    }

    bool IsValidMove(Vector3 targetPosition)
    {
        Vector2Int targetCell = WorldToCell(targetPosition);

        switch (pieceType)
        {
            case PieceType.Pawn:
                return Pawn.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    hasMoved,
                    ChessBoard.Instance.GetPieceAt
                );
            case PieceType.Knight:     // this case is pieceType knight and will activate the IsValidMove embedded in the knight script 
                return Knight.IsValidMove(
                    currentCell,
                    targetCell,
                    team,
                    ChessBoard.Instance.GetPieceAt 
                );

            // Add other piece cases later...

            default:
                return false;
        }
    }

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        float cellSize = 0.5f;
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int y = Mathf.FloorToInt(worldPos.y / cellSize);
        return new Vector2Int(x, y);
    }
}
