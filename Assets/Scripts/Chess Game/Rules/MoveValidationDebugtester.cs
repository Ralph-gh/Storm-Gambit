using UnityEngine;

public class MoveValidationDebugTester : MonoBehaviour
{
    [Header("Move Validation Test")]

    [SerializeField]
    private Vector2Int fromSquare;

    [SerializeField]
    private Vector2Int destinationSquare;

    [ContextMenu("Test Legal Chess Move")]
    public void TestLegalChessMove()
    {
        if (ChessBoard.Instance == null)
        {
            Debug.LogError(
                "[MOVE VALIDATOR TEST] ChessBoard missing."
            );

            return;
        }

        ChessPiece piece =
            ChessBoard.Instance.GetPieceAt(
                fromSquare
            );

        if (piece == null)
        {
            Debug.LogError(
                $"[MOVE VALIDATOR TEST] " +
                $"No piece at {fromSquare}."
            );

            return;
        }

        ChessMoveValidationResult result =
            ChessMoveValidator.Validate(
                piece,
                destinationSquare
            );

        if (result.IsLegal)
        {
            Debug.Log(
                $"[MOVE VALIDATOR TEST] LEGAL: " +
                $"{piece.team} {piece.pieceType} " +
                $"{fromSquare} -> {destinationSquare}"
            );
        }
        else
        {
            Debug.Log(
                $"[MOVE VALIDATOR TEST] ILLEGAL: " +
                $"{piece.team} {piece.pieceType} " +
                $"{fromSquare} -> {destinationSquare} | " +
                $"Reason = {result.Reason} | " +
                $"{result.Message}"
            );
        }
    }
}