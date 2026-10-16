using UnityEngine;

public class PositionSimulationDebugTester : MonoBehaviour
{
    [Header("Simulation Test")]

    [SerializeField]
    private Vector2Int fromSquare;

    [SerializeField]
    private Vector2Int destinationSquare;

    [ContextMenu("Test Simulated Move")]
    public void TestSimulatedMove()
    {
        if (ChessBoard.Instance == null)
        {
            Debug.LogError(
                "[SIM TEST] ChessBoard not found."
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
                $"[SIM TEST] No piece found at " +
                $"{fromSquare}."
            );

            return;
        }

        bool leavesKingInCheck =
            ChessPositionSimulator
                .WouldLeaveKingInCheck(
                    piece,
                    destinationSquare
                );

        if (leavesKingInCheck)
        {
            Debug.Log(
                $"[SIM TEST] {piece.team} " +
                $"{piece.pieceType} " +
                $"{fromSquare} -> {destinationSquare} " +
                $"WOULD LEAVE KING IN CHECK."
            );
        }
        else
        {
            Debug.Log(
                $"[SIM TEST] {piece.team} " +
                $"{piece.pieceType} " +
                $"{fromSquare} -> {destinationSquare} " +
                $"leaves king SAFE."
            );
        }
    }
}